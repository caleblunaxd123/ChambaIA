using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Matching;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Api.Infrastructure;

public static class ExplanationBuilder
{
    /// <summary>
    /// Loads the caller's profile and preferences and explains the stored match. The stored semantic score is reused, so the
    /// explanation never calls the embedding server; it is one cheap, deterministic engine run per detail view.
    /// </summary>
    public static async Task<MatchExplanation?> BuildAsync(AppDbContext db, Guid userId, JobOffer job, CandidateJobMatch match, CancellationToken ct)
    {
        var profile = await db.CandidateProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
        var prefs = await db.JobPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
        return profile is null || prefs is null ? null : MatchExplainer.Explain(profile, prefs, job, match.SemanticScore);
    }
}
