using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Matching;
using ChambaIA.Infrastructure.Embeddings;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

namespace ChambaIA.Infrastructure.Matching;

public sealed record RecomputeResult(int Evaluated, int Created, int Updated);

/// <summary>
/// Runs the matching engine and upserts <see cref="CandidateJobMatch"/> rows. A user's own reaction (status) is never reset.
/// Three entry points, from the widest to the cheapest: everything for one user (their preferences changed), everyone against
/// everything (embeddings caught up) and everyone against only the offers that just arrived or changed (ingestion).
/// </summary>
public sealed class MatchRecomputeService(AppDbContext db, EmbeddingService embeddings, IOptions<EmbeddingOptions> embeddingOptions, TimeProvider clock)
{
    public async Task<RecomputeResult> RecomputeForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await db.CandidateProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        var prefs = await db.JobPreferences.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null || prefs is null) return new RecomputeResult(0, 0, 0);

        var now = clock.GetUtcNow();
        var jobs = await db.JobOffers
            .Where(j => j.IsActive && (j.ExpiresAt == null || j.ExpiresAt > now))
            .ToListAsync(ct);

        return await EvaluateAsync(profile, prefs, jobs, jobIds: null, ct);
    }

    /// <summary>Rematches every candidate (after embeddings caught up). Tracking is cleared between users to keep memory flat.</summary>
    public async Task<int> RecomputeAllAsync(CancellationToken ct = default)
    {
        var userIds = await db.CandidateProfiles
            .Where(p => db.JobPreferences.Any(pref => pref.UserId == p.UserId))
            .Select(p => p.UserId)
            .ToListAsync(ct);

        foreach (var userId in userIds)
        {
            await RecomputeForUserAsync(userId, ct);
            db.ChangeTracker.Clear();
        }
        return userIds.Count;
    }

    /// <summary>
    /// Incremental: evaluates only <paramref name="jobIds"/> for every candidate. After an ingestion that brought 3 new offers
    /// to a catalogue of 10,000 this is 3 evaluations per user instead of 10,000.
    /// </summary>
    /// <returns>Number of candidates evaluated.</returns>
    public async Task<int> RecomputeForJobsAsync(IReadOnlyCollection<Guid> jobIds, CancellationToken ct = default)
    {
        if (jobIds.Count == 0) return 0;

        var now = clock.GetUtcNow();
        var jobs = await db.JobOffers
            .AsNoTracking()
            .Where(j => jobIds.Contains(j.Id) && j.IsActive && (j.ExpiresAt == null || j.ExpiresAt > now))
            .ToListAsync(ct);
        if (jobs.Count == 0) return 0;

        var userIds = await db.CandidateProfiles
            .Where(p => db.JobPreferences.Any(pref => pref.UserId == p.UserId))
            .Select(p => p.UserId)
            .ToListAsync(ct);

        var ids = jobs.Select(j => j.Id).ToList();
        foreach (var userId in userIds)
        {
            var profile = await db.CandidateProfiles.SingleAsync(p => p.UserId == userId, ct);
            var prefs = await db.JobPreferences.SingleAsync(p => p.UserId == userId, ct);
            await EvaluateAsync(profile, prefs, jobs, ids, ct);
            db.ChangeTracker.Clear();
        }
        return userIds.Count;
    }

    private async Task<RecomputeResult> EvaluateAsync(CandidateProfile profile, JobPreferences prefs, IReadOnlyList<JobOffer> jobs, IReadOnlyCollection<Guid>? jobIds, CancellationToken ct)
    {
        // Stage C is optional: with no embeddings (provider off/down, profile too empty) every job is scored by stages A and B only.
        var semantic = await SemanticScoresAsync(profile, prefs, jobIds, ct);
        var now = clock.GetUtcNow();

        var existing = await db.Matches
            .Where(m => m.CandidateId == profile.Id && (jobIds == null || jobIds.Contains(m.JobId)))
            .ToDictionaryAsync(m => m.JobId, ct);

        int created = 0, updated = 0;
        foreach (var job in jobs)
        {
            var outcome = MatchEngine.Evaluate(profile, prefs, job, semantic.TryGetValue(job.Id, out var s) ? s : null);

            if (!existing.TryGetValue(job.Id, out var match))
            {
                match = new CandidateJobMatch { CandidateId = profile.Id, JobId = job.Id, CreatedAt = now };
                db.Matches.Add(match);
                created++;
            }
            else updated++;

            Apply(match, outcome, now);
        }

        await db.SaveChangesAsync(ct);
        return new RecomputeResult(jobs.Count, created, updated);
    }

    /// <summary>
    /// Cosine distance computed inside PostgreSQL by pgvector (and served by the HNSW index when the query is a top-K), so the
    /// vectors never travel to the app. Only offers that already have an embedding get a score.
    /// </summary>
    private async Task<Dictionary<Guid, double>> SemanticScoresAsync(CandidateProfile profile, JobPreferences prefs, IReadOnlyCollection<Guid>? jobIds, CancellationToken ct)
    {
        if (!embeddings.IsEnabled) return [];
        if (!await embeddings.EnsureProfileEmbeddingAsync(profile, prefs, ct) || profile.ProfileEmbedding is not { } query) return [];

        var o = embeddingOptions.Value;
        var distances = await db.JobOffers
            .Where(j => j.IsActive && j.Embedding != null && (jobIds == null || jobIds.Contains(j.Id)))
            .Select(j => new { j.Id, Distance = j.Embedding!.CosineDistance(query) })
            .ToListAsync(ct);

        return distances.ToDictionary(
            x => x.Id,
            x => SemanticSimilarity.ToScore(1 - x.Distance, o.UnrelatedCosine, o.IdenticalKindCosine));
    }

    private static void Apply(CandidateJobMatch match, MatchOutcome outcome, DateTimeOffset now)
    {
        match.RulesScore = outcome.RulesScore;
        match.SkillsScore = outcome.SkillsScore;
        match.SemanticScore = outcome.SemanticScore;
        match.OverallScore = outcome.OverallScore;
        match.Category = outcome.Category;
        match.MatchedSkills = outcome.MatchedSkills;
        match.MissingSkills = outcome.MissingSkills;
        match.Reasons = outcome.Reasons;
        match.Warnings = outcome.Warnings;
        match.UpdatedAt = now;
    }
}
