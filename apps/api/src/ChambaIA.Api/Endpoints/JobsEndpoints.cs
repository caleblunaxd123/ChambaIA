using ChambaIA.Api.Contracts;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Domain.Enums;
using ChambaIA.Infrastructure.Persistence;
using Pgvector.EntityFrameworkCore;
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
            var ordered = parsed.Sort == FeedSort.Salary
                ? query.OrderBy(j => (j.SalaryMax ?? j.SalaryMin) == null).ThenByDescending(j => j.SalaryMax ?? j.SalaryMin).ThenByDescending(j => j.PostedAt).ThenBy(j => j.Id)
                : query.OrderByDescending(j => j.PostedAt).ThenBy(j => j.Id);
            var jobs = await ordered
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

            var explanation = match is null ? null : await ExplanationBuilder.BuildAsync(db, userId, job, match, ct);
            return Results.Ok(new JobDetailResponse(job.ToDetail(), match?.ToDetail(explanation), application?.ToDto()));
        });

        // Offers that mean the same thing. With embeddings, pgvector's nearest neighbours (HNSW) by cosine distance; without
        // them, the same company or sector, newest first. Offers the caller already dismissed are not suggested again.
        group.MapGet("/{id:guid}/similar", async (Guid id, int? limit, HttpContext http, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var job = await db.JobOffers.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id, ct);
            if (job is null) return Results.NotFound();

            var take = Math.Clamp(limit ?? 5, 1, 10);
            var now = clock.GetUtcNow();
            var candidates = db.JobOffers.AsNoTracking()
                .Where(j => j.IsActive && j.Id != id && (j.ExpiresAt == null || j.ExpiresAt > now))
                .Where(j => !db.Matches.Any(m => m.JobId == j.Id && m.Status == MatchStatus.Dismissed
                    && db.CandidateProfiles.Any(p => p.Id == m.CandidateId && p.UserId == userId)));

            var similar = job.Embedding is { } vector
                ? await candidates.Where(j => j.Embedding != null).OrderBy(j => j.Embedding!.CosineDistance(vector)).Take(take).ToListAsync(ct)
                : await candidates
                    .Where(j => j.NormalizedCompany == job.NormalizedCompany || (job.Industry != null && j.Industry == job.Industry))
                    .OrderByDescending(j => j.PostedAt).Take(take).ToListAsync(ct);

            var ids = similar.Select(j => j.Id).ToList();
            var matches = await db.Matches.AsNoTracking()
                .Where(m => ids.Contains(m.JobId) && db.CandidateProfiles.Any(p => p.Id == m.CandidateId && p.UserId == userId))
                .ToDictionaryAsync(m => m.JobId, ct);

            return Results.Ok(similar.Select(j => new FeedItemDto(j.ToSummary(), matches.TryGetValue(j.Id, out var m) ? m.ToSummary() : null)).ToList());
        });

        return api;
    }
}
