using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Ingestion;
using ChambaIA.Infrastructure.Embeddings;
using ChambaIA.Infrastructure.Matching;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChambaIA.Infrastructure.Ingestion;

public sealed class IngestionOptions
{
    public const string Section = "Ingestion";

    /// <summary>Serve the fictional demo offers through the pipeline (Development only).</summary>
    public bool IncludeDemo { get; set; }
    /// <summary>Offers a healthy source has stopped listing for this long are considered closed.</summary>
    public int StaleAfterDays { get; set; } = 7;
    public List<JsonFeedOptions> Feeds { get; set; } = [];
}

public sealed record SourceReport(string Key, int Fetched, int Created, int Updated, int Unchanged, int Duplicates, int Invalid, string? Error);

public sealed record IngestionReport(IReadOnlyList<SourceReport> Sources, int Deactivated, int UsersRecomputed)
{
    public int Created => Sources.Sum(s => s.Created);
    public int Changed => Sources.Sum(s => s.Created + s.Updated) + Deactivated;
}

/// <summary>
/// FUENTES → NORMALIZACIÓN → DEDUPLICACIÓN → ALMACÉN → MATCHING. Idempotent: running it twice in a row changes nothing
/// the second time. Deduplication has three layers, cheapest first:
/// 1. <c>(SourceId, ExternalId)</c>: the same source re-listing an offer updates it in place.
/// 2. Content hash: the identical offer published by another source.
/// 3. Similarity (<see cref="DuplicateDetector"/>): the same company and role reworded on another portal.
/// Duplicates are stored inactive with <see cref="JobOffer.DuplicateOfId"/>, so they are never shown or matched twice.
/// </summary>
public sealed class IngestionService(
    AppDbContext db,
    IEnumerable<IJobSource> sources,
    MatchRecomputeService matcher,
    EmbeddingService embeddings,
    IOptions<IngestionOptions> options,
    TimeProvider clock,
    ILogger<IngestionService> logger)
{
    /// <summary>Runs every registered source, retires stale offers and refreshes everyone's matches if anything changed.</summary>
    public Task<IngestionReport> RunAsync(CancellationToken ct = default) => RunAsync(sources, recompute: true, ct);

    public async Task<IngestionReport> RunAsync(IEnumerable<IJobSource> toRun, bool recompute, CancellationToken ct = default)
    {
        var reports = new List<SourceReport>();
        var healthy = new List<Guid>();
        var changedJobs = new HashSet<Guid>(); // offers that are new or changed: the only ones worth re-matching

        foreach (var source in toRun)
        {
            var (report, sourceId) = await RunSourceAsync(source, changedJobs, ct);
            reports.Add(report);
            if (report.Error is null) healthy.Add(sourceId);
            db.ChangeTracker.Clear();
        }

        var deactivated = await DeactivateStaleAsync(healthy, ct);

        // Stage C: give new/changed offers their vector before anyone is matched. No-op when embeddings are off or the server is down.
        await embeddings.EmbedPendingJobsAsync(ct: ct);

        // Incremental: retired offers need no work (feeds only show active ones); new/changed ones are evaluated for every user.
        var users = 0;
        if (recompute && changedJobs.Count > 0)
            users = await matcher.RecomputeForJobsAsync(changedJobs, ct);

        var result = new IngestionReport(reports, deactivated, users);
        logger.LogInformation(
            "Ingestion finished: {Created} new, {Updated} updated, {Duplicates} duplicates, {Invalid} invalid, {Deactivated} retired, {Users} users rematched.",
            result.Created, reports.Sum(r => r.Updated), reports.Sum(r => r.Duplicates), reports.Sum(r => r.Invalid), deactivated, users);
        return result;
    }

    private async Task<(SourceReport Report, Guid SourceId)> RunSourceAsync(IJobSource connector, ISet<Guid> changedJobs, CancellationToken ct)
    {
        var source = await EnsureSourceAsync(connector, ct);
        if (!source.IsEnabled) return (new SourceReport(connector.Key, 0, 0, 0, 0, 0, 0, "Fuente desactivada."), source.Id);

        IReadOnlyList<RawJob> raws;
        try
        {
            raws = await connector.FetchJobsAsync(source.LastFetchedAt, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // One broken source never stops the others; the error is kept for operators.
            logger.LogWarning(ex, "Source {Key} failed.", connector.Key);
            source.LastError = Truncate(ex.Message, 500);
            await db.SaveChangesAsync(ct);
            return (new SourceReport(connector.Key, 0, 0, 0, 0, 0, 0, source.LastError), source.Id);
        }

        try
        {
            return (await StoreAsync(connector, source, raws, changedJobs, ct), source.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // A batch that cannot be stored is dropped whole (nothing half-written) and the next source still runs.
            logger.LogError(ex, "Source {Key}: could not store the fetched offers.", connector.Key);
            db.ChangeTracker.Clear();
            var error = Truncate($"No se pudieron guardar las ofertas: {ex.GetBaseException().Message}", 500);
            await db.JobSources.Where(s => s.Id == source.Id).ExecuteUpdateAsync(u => u.SetProperty(s => s.LastError, error), ct);
            return (new SourceReport(connector.Key, raws.Count, 0, 0, 0, 0, 0, error), source.Id);
        }
    }

    private async Task<SourceReport> StoreAsync(IJobSource connector, JobSource source, IReadOnlyList<RawJob> raws, ISet<Guid> changedJobs, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var existing = await db.JobOffers.Where(j => j.SourceId == source.Id).ToDictionaryAsync(j => j.ExternalId, ct);
        var addedThisRun = new List<JobOffer>();
        int created = 0, updated = 0, unchanged = 0, duplicates = 0, invalid = 0;

        foreach (var raw in raws)
        {
            var result = JobNormalizer.Normalize(raw, now);
            if (result.Job is not { } job)
            {
                invalid++;
                logger.LogDebug("Source {Key}: skipped {Id}: {Error}", connector.Key, raw.ExternalId, result.Error);
                continue;
            }

            if (existing.TryGetValue(job.ExternalId, out var offer))
            {
                // Layer 1: same source, same id → update in place (and revive it if it had been retired).
                var changed = offer.ContentHash != job.ContentHash || !offer.IsActive && offer.DuplicateOfId is null;
                var wasDuplicate = offer.DuplicateOfId is not null;
                job.ApplyTo(offer, source.Name, now);
                offer.IsActive = !wasDuplicate;
                if (changed)
                {
                    updated++;
                    if (offer.IsActive) changedJobs.Add(offer.Id);
                }
                else unchanged++;
                continue;
            }

            offer = new JobOffer { SourceId = source.Id, FirstSeenAt = now };
            job.ApplyTo(offer, source.Name, now);

            var original = await FindOriginalAsync(offer, source.Id, addedThisRun, ct);
            if (original is not null)
            {
                offer.IsActive = false;
                offer.DuplicateOfId = original.Id;
                original.LastSeenAt = now; // still being advertised somewhere: keep it alive
                duplicates++;
            }
            else
            {
                addedThisRun.Add(offer);
                created++;
                changedJobs.Add(offer.Id);
            }

            db.JobOffers.Add(offer);
            existing[offer.ExternalId] = offer;
        }

        source.LastFetchedAt = now;
        source.LastError = null;
        source.Name = connector.Name;
        await db.SaveChangesAsync(ct);

        return new SourceReport(connector.Key, raws.Count, created, updated, unchanged, duplicates, invalid, null);
    }

    /// <summary>Layers 2 and 3 of dedup, against active offers of other sources and offers added earlier in this run.</summary>
    private async Task<JobOffer?> FindOriginalAsync(JobOffer offer, Guid sourceId, List<JobOffer> addedThisRun, CancellationToken ct)
    {
        var inRun = addedThisRun.FirstOrDefault(o => o.ContentHash == offer.ContentHash)
            ?? addedThisRun.FirstOrDefault(o => DuplicateDetector.LooksLikeSameOffer(Fingerprint(o), Fingerprint(offer)));
        if (inRun is not null) return inRun;

        var byHash = await db.JobOffers
            .Where(j => j.IsActive && j.DuplicateOfId == null && j.SourceId != sourceId && j.ContentHash == offer.ContentHash)
            .OrderBy(j => j.FirstSeenAt)
            .FirstOrDefaultAsync(ct);
        if (byHash is not null) return byHash;

        // Similarity candidates share the normalised company (indexed), so this stays a small query.
        var sameCompany = await db.JobOffers
            .Where(j => j.IsActive && j.DuplicateOfId == null && j.SourceId != sourceId && j.NormalizedCompany == offer.NormalizedCompany)
            .OrderByDescending(j => j.PostedAt)
            .Take(200)
            .ToListAsync(ct);
        return sameCompany.FirstOrDefault(j => DuplicateDetector.LooksLikeSameOffer(Fingerprint(j), Fingerprint(offer)));
    }

    private static OfferFingerprint Fingerprint(JobOffer j) =>
        new(j.NormalizedCompany, j.Title, j.District, j.SalaryMin, j.SalaryMax, j.PostedAt);

    /// <summary>Closes offers past their expiry and those a healthy source stopped listing a while ago.</summary>
    private async Task<int> DeactivateStaleAsync(IReadOnlyCollection<Guid> healthySources, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var staleBefore = now.AddDays(-Math.Max(1, options.Value.StaleAfterDays));

        var expired = await db.JobOffers
            .Where(j => j.IsActive && j.ExpiresAt != null && j.ExpiresAt <= now)
            .ExecuteUpdateAsync(u => u.SetProperty(j => j.IsActive, false), ct);

        var abandoned = healthySources.Count == 0 ? 0 : await db.JobOffers
            .Where(j => j.IsActive && healthySources.Contains(j.SourceId) && j.LastSeenAt < staleBefore)
            .ExecuteUpdateAsync(u => u.SetProperty(j => j.IsActive, false), ct);

        return expired + abandoned;
    }

    private async Task<int> RecomputeAllAsync(CancellationToken ct)
    {
        var userIds = await db.CandidateProfiles
            .Where(p => db.JobPreferences.Any(pref => pref.UserId == p.UserId))
            .Select(p => p.UserId)
            .ToListAsync(ct);

        foreach (var userId in userIds)
        {
            await matcher.RecomputeForUserAsync(userId, ct);
            db.ChangeTracker.Clear();
        }
        return userIds.Count;
    }

    private async Task<JobSource> EnsureSourceAsync(IJobSource connector, CancellationToken ct)
    {
        var source = await db.JobSources.SingleOrDefaultAsync(s => s.Key == connector.Key, ct);
        if (source is not null) return source;

        source = new JobSource { Key = connector.Key, Name = connector.Name, Kind = connector.Kind, BaseUrl = connector.BaseUrl };
        db.JobSources.Add(source);
        await db.SaveChangesAsync(ct);
        return source;
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
