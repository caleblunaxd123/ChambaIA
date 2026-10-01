using ChambaIA.Api.Contracts;
using ChambaIA.Domain.Enums;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Api.Infrastructure;

public static class MatchOverviewQuery
{
    public static async Task<MatchOverviewDto> GetAsync(AppDbContext db, Guid candidateId, DateTimeOffset now, CancellationToken ct)
    {
        var live = db.Matches.AsNoTracking()
            .Where(m => m.CandidateId == candidateId && m.Status != MatchStatus.Dismissed && m.Status != MatchStatus.Rejected)
            .Where(m => m.Job!.IsActive && (m.Job.ExpiresAt == null || m.Job.ExpiresAt > now));

        var rows = await live
            .GroupBy(m => new { m.Category, IsNew = m.Status == MatchStatus.New })
            .Select(g => new { g.Key.Category, g.Key.IsNew, Count = g.Count(), Last = g.Max(m => m.UpdatedAt) })
            .ToListAsync(ct);

        int Count(Func<MatchCategory, bool> category, bool? isNew = null) =>
            rows.Where(x => category(x.Category) && (isNew is null || x.IsNew == isNew)).Sum(x => x.Count);

        static bool Strong(MatchCategory c) => c is MatchCategory.Excellent or MatchCategory.VeryCompatible;
        static bool Possible(MatchCategory c) => c is MatchCategory.Compatible or MatchCategory.Review;

        return new MatchOverviewDto(
            NewTotal: Count(_ => true, true),
            NewStrong: Count(Strong, true),
            NewPossible: Count(Possible, true),
            NewNotRecommended: Count(c => c == MatchCategory.Poor, true),
            Strong: Count(Strong),
            Possible: Count(c => c == MatchCategory.Compatible),
            Review: Count(c => c == MatchCategory.Review),
            NotRecommended: Count(c => c == MatchCategory.Poor),
            LastUpdatedAt: rows.Count == 0 ? null : rows.Max(x => x.Last));
    }
}
