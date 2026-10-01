using ChambaIA.Api.Contracts;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Api.Endpoints;

public static class JobsEndpoints
{
    public static RouteGroupBuilder MapJobs(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/jobs").WithTags("Jobs").RequireAuthorization();

        // Everything the system has ingested, newest first, annotated with the caller's match when one exists.
        group.MapGet("", async ([AsParameters] JobFilterQuery filter, HttpContext http, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            if (!filter.TryParse(out var parsed, out var error)) return error!;
            var (page, pageSize) = (parsed.Page, parsed.PageSize);

            var query = db.JobOffers.AsNoTracking().ApplyFilters(parsed, clock.GetUtcNow());
            var total = await query.CountAsync(ct);
            var jobs = await query
                .OrderByDescending(j => j.PostedAt).ThenBy(j => j.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(ct);

            var ids = jobs.Select(j => j.Id).ToList();
            var matches = await db.Matches.AsNoTracking()
                .Where(m => ids.Contains(m.JobId) && db.CandidateProfiles.Any(p => p.Id == m.CandidateId && p.UserId == userId))
                .ToDictionaryAsync(m => m.JobId, ct);

            var items = jobs.Select(j => new FeedItemDto(j.ToSummary(), matches.TryGetValue(j.Id, out var m) ? m.ToSummary() : null)).ToList();
            return Results.Ok(new PagedResult<FeedItemDto>(items, page, pageSize, total, page * pageSize < total));
        });

        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var job = await db.JobOffers.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id, ct);
            if (job is null) return Results.NotFound();

            var match = await db.Matches.AsNoTracking()
                .SingleOrDefaultAsync(m => m.JobId == id && db.CandidateProfiles.Any(p => p.Id == m.CandidateId && p.UserId == userId), ct);
            var application = await db.Applications.AsNoTracking()
                .SingleOrDefaultAsync(a => a.JobId == id && a.UserId == userId, ct);

            return Results.Ok(new JobDetailResponse(job.ToDetail(), match?.ToDetail(), application?.ToDto()));
        });

        return api;
    }
}
