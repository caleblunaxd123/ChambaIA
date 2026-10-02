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
/// Runs the deterministic matching engine for one candidate against every active offer and upserts
/// <see cref="CandidateJobMatch"/> rows. A user's own reaction (status) is never reset by a recompute.
/// </summary>
public sealed class MatchRecomputeService(AppDbContext db, EmbeddingService embeddings, IOptions<EmbeddingOptions> embeddingOptions, TimeProvider clock)
{
    public async Task<RecomputeResult> RecomputeForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await db.CandidateProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        var prefs = await db.JobPreferences.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null || prefs is null) return new RecomputeResult(0, 0, 0);

        // Stage C is optional: with no embeddings (provider off/down, profile too empty) every job is scored by stages A and B only.
        var semantic = await SemanticScoresAsync(profile, prefs, ct);

        var now = clock.GetUtcNow();
        var jobs = await db.JobOffers
            .Where(j => j.IsActive && (j.ExpiresAt == null || j.ExpiresAt > now))
            .ToListAsync(ct);

        var existing = await db.Matches
            .Where(m => m.CandidateId == profile.Id)
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

    /// <summary>Rematches every candidate (after ingestion or after embeddings caught up). Tracking is cleared between users to keep memory flat.</summary>
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
    /// Cosine distance computed inside PostgreSQL by pgvector (and served by the HNSW index when the query is a top-K), so the
    /// vectors never travel to the app. Only offers that already have an embedding get a score.
    /// </summary>
    private async Task<Dictionary<Guid, double>> SemanticScoresAsync(CandidateProfile profile, JobPreferences prefs, CancellationToken ct)
    {
        if (!embeddings.IsEnabled) return [];
        if (!await embeddings.EnsureProfileEmbeddingAsync(profile, prefs, ct) || profile.ProfileEmbedding is not { } query) return [];

        var o = embeddingOptions.Value;
        var distances = await db.JobOffers
            .Where(j => j.IsActive && j.Embedding != null)
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
