using ChambaIA.Domain.Enums;
using ChambaIA.Infrastructure.Ingestion;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Api.Endpoints;

public static class SourcesEndpoints
{
    public sealed record SourceDto(string Key, string Name, JobSourceKind Kind, DateTimeOffset? LastFetchedAt, int ActiveOffers, bool Healthy);

    /// <summary>
    /// Transparency: where the offers in the feed come from and how fresh they are. Error details stay in the logs and
    /// the database (operators only); candidates just see whether a source is working.
    /// </summary>
    public static RouteGroupBuilder MapSources(this RouteGroupBuilder api, bool isDevelopment)
    {
        var group = api.MapGroup("/sources").WithTags("Sources").RequireAuthorization();

        group.MapGet("", async (AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var now = clock.GetUtcNow();
            var sources = await db.JobSources.AsNoTracking()
                .Where(s => s.IsEnabled)
                .Select(s => new SourceDto(
                    s.Key, s.Name, s.Kind, s.LastFetchedAt,
                    db.JobOffers.Count(j => j.SourceId == s.Id && j.IsActive && (j.ExpiresAt == null || j.ExpiresAt > now)),
                    s.LastError == null))
                .ToListAsync(ct);
            return Results.Ok(sources.OrderByDescending(s => s.ActiveOffers).ToList());
        });

        // Manual trigger for local development; in production the worker runs ingestion on its schedule.
        if (isDevelopment)
        {
            group.MapPost("/ingest", async (IngestionService ingestion, CancellationToken ct) => Results.Ok(await ingestion.RunAsync(ct)));
        }

        return api;
    }
}
