using ChambaIA.Api.Contracts;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Domain.Enums;
using ChambaIA.Infrastructure.Matching;
using ChambaIA.Infrastructure.Persistence;
using ChambaIA.Infrastructure.Tracking;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Api.Endpoints;

public static class MatchesEndpoints
{
    public static RouteGroupBuilder MapMatches(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/matches").WithTags("Matches").RequireAuthorization();

        group.MapGet("", async ([AsParameters] MatchFeedQuery filter, HttpContext http, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var candidateId = await CandidateIdAsync(db, http.User.GetUserId(), ct);
            if (candidateId is null) return Results.NotFound();

            if (!filter.TryParse(out var parsed, out var error)) return error!;
            var (page, pageSize) = (parsed.Page, parsed.PageSize);
            var jobs = db.JobOffers.ApplyFilters(parsed, clock.GetUtcNow());

            var rows = db.Matches.AsNoTracking()
                .Where(m => m.CandidateId == candidateId)
                .Join(jobs, m => m.JobId, j => j.Id, (m, j) => new { Match = m, Job = j });

            rows = parsed.Tab switch
            {
                MatchTab.New => rows
                    .Where(r => r.Match.Status == MatchStatus.New && r.Match.Category != MatchCategory.Poor)
                    .OrderByDescending(r => r.Job.PostedAt),
                MatchTab.Saved => rows
                    .Where(r => r.Match.Status == MatchStatus.Interested)
                    .OrderByDescending(r => r.Match.UpdatedAt),
                _ => rows
                    .Where(r => r.Match.Category != MatchCategory.Poor && r.Match.Status != MatchStatus.Dismissed && r.Match.Status != MatchStatus.Rejected)
                    .OrderByDescending(r => r.Match.OverallScore).ThenByDescending(r => r.Job.PostedAt)
            };

            if (parsed.Category is { } category) rows = rows.Where(r => r.Match.Category == category);

            // An explicit sort always wins over the tab's natural order.
            if (parsed.Sort is { } sort)
                rows = sort switch
                {
                    FeedSort.Recent => rows.OrderByDescending(r => r.Job.PostedAt).ThenByDescending(r => r.Match.OverallScore),
                    FeedSort.Salary => rows.OrderByDescending(r => r.Job.SalaryMax ?? r.Job.SalaryMin).ThenByDescending(r => r.Match.OverallScore),
                    _ => rows.OrderByDescending(r => r.Match.OverallScore).ThenByDescending(r => r.Job.PostedAt)
                };

            var total = await rows.CountAsync(ct);
            var page_ = await rows.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

            var items = page_.Select(r => new FeedItemDto(r.Job.ToSummary(), r.Match.ToSummary())).ToList();
            return Results.Ok(new PagedResult<FeedItemDto>(items, page, pageSize, total, page * pageSize < total));
        });

        group.MapGet("/overview", async (HttpContext http, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var candidateId = await CandidateIdAsync(db, http.User.GetUserId(), ct);
            if (candidateId is null) return Results.NotFound();

            return Results.Ok(await MatchOverviewQuery.GetAsync(db, candidateId.Value, clock.GetUtcNow(), ct));
        });

        group.MapGet("/{jobId:guid}", async (Guid jobId, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var match = await db.Matches.AsNoTracking()
                .Include(m => m.Job)
                .SingleOrDefaultAsync(m => m.JobId == jobId && db.CandidateProfiles.Any(p => p.Id == m.CandidateId && p.UserId == userId), ct);
            if (match?.Job is null) return Results.NotFound();
            var explanation = await ExplanationBuilder.BuildAsync(db, userId, match.Job, match, ct);
            return Results.Ok(new MatchDetailResponse(match.Job.ToSummary(), match.ToDetail(explanation)));
        });

        group.MapPost("/refresh", async (HttpContext http, MatchRecomputeService matcher, CancellationToken ct) =>
        {
            var result = await matcher.RecomputeForUserAsync(http.User.GetUserId(), ct);
            return Results.Ok(result);
        });

        group.MapPost("/{jobId:guid}/seen", async (Guid jobId, HttpContext http, TrackerService tracker, CancellationToken ct) =>
            await tracker.MarkSeenAsync(http.User.GetUserId(), jobId, ct) is null ? Results.NotFound() : Results.NoContent());

        group.MapPost("/{jobId:guid}/interested", async (Guid jobId, HttpContext http, TrackerService tracker, CancellationToken ct) =>
            await tracker.MarkInterestedAsync(http.User.GetUserId(), jobId, ct) is { } m ? Results.Ok(m.ToSummary()) : Results.NotFound());

        group.MapPost("/{jobId:guid}/dismiss", async (Guid jobId, HttpContext http, TrackerService tracker, CancellationToken ct) =>
            await tracker.DismissAsync(http.User.GetUserId(), jobId, ct) is { } m ? Results.Ok(m.ToSummary()) : Results.NotFound());

        // Undo for "me interesa" and "descartar" (the app offers it right after either action).
        group.MapPost("/{jobId:guid}/reset", async (Guid jobId, HttpContext http, TrackerService tracker, CancellationToken ct) =>
            await tracker.ResetAsync(http.User.GetUserId(), jobId, ct) is { } m ? Results.Ok(m.ToSummary()) : Results.NotFound());

        return api;
    }

    private static Task<Guid?> CandidateIdAsync(AppDbContext db, Guid userId, CancellationToken ct) =>
        db.CandidateProfiles.Where(p => p.UserId == userId).Select(p => (Guid?)p.Id).SingleOrDefaultAsync(ct);
}
