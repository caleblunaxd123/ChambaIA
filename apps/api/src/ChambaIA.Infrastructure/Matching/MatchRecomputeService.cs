using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Matching;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Infrastructure.Matching;

public sealed record RecomputeResult(int Evaluated, int Created, int Updated);

/// <summary>
/// Runs the deterministic matching engine for one candidate against every active offer and upserts
/// <see cref="CandidateJobMatch"/> rows. A user's own reaction (status) is never reset by a recompute.
/// </summary>
public sealed class MatchRecomputeService(AppDbContext db, TimeProvider clock)
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

        var existing = await db.Matches
            .Where(m => m.CandidateId == profile.Id)
            .ToDictionaryAsync(m => m.JobId, ct);

        int created = 0, updated = 0;
        foreach (var job in jobs)
        {
            var outcome = MatchEngine.Evaluate(profile, prefs, job);

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

    private static void Apply(CandidateJobMatch match, MatchOutcome outcome, DateTimeOffset now)
    {
        match.RulesScore = outcome.RulesScore;
        match.SkillsScore = outcome.SkillsScore;
        match.OverallScore = outcome.OverallScore;
        match.Category = outcome.Category;
        match.MatchedSkills = outcome.MatchedSkills;
        match.MissingSkills = outcome.MissingSkills;
        match.Reasons = outcome.Reasons;
        match.Warnings = outcome.Warnings;
        match.UpdatedAt = now;
    }
}
