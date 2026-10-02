using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Matching;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;

namespace ChambaIA.Infrastructure.Embeddings;

public sealed record EmbedJobsResult(int Embedded, int Failed, int Remaining)
{
    public static readonly EmbedJobsResult None = new(0, 0, 0);
}

/// <summary>
/// Decides what needs a vector and stores it. Embeddings are an *enhancement*: every method here is safe to call when the
/// provider is off or down (it logs, returns "nothing done" and the caller carries on with deterministic matching).
/// </summary>
public sealed class EmbeddingService(
    AppDbContext db,
    IEmbeddingProvider provider,
    IOptions<EmbeddingOptions> options,
    TimeProvider clock,
    ILogger<EmbeddingService> logger)
{
    private readonly EmbeddingOptions _o = options.Value;

    public bool IsEnabled => provider.IsEnabled;

    /// <summary>Embeds active offers that have no vector or whose content changed since. Processes at most <paramref name="max"/>.</summary>
    public async Task<EmbedJobsResult> EmbedPendingJobsAsync(int max = 256, CancellationToken ct = default)
    {
        if (!provider.IsEnabled) return EmbedJobsResult.None;

        var now = clock.GetUtcNow();
        var pending = await db.JobOffers
            .Where(j => j.IsActive && (j.ExpiresAt == null || j.ExpiresAt > now))
            .Where(j => j.Embedding == null || j.EmbeddingHash == null)
            .OrderByDescending(j => j.PostedAt)
            .Take(max)
            .ToListAsync(ct);

        // Content changed after it was embedded: the hash of the current text no longer matches the stored one.
        var stale = await db.JobOffers
            .Where(j => j.IsActive && j.Embedding != null && j.EmbeddingHash != null)
            .OrderBy(j => j.LastSeenAt)
            .Take(max)
            .ToListAsync(ct);
        pending.AddRange(stale.Where(j => j.EmbeddingHash != EmbeddingText.Hash(EmbeddingText.ForJob(j), provider.Model)).Take(Math.Max(0, max - pending.Count)));

        int embedded = 0, failed = 0;
        foreach (var batch in pending.Chunk(Math.Max(1, _o.BatchSize)))
        {
            var texts = batch.Select(EmbeddingText.ForJob).ToList();
            try
            {
                var vectors = await provider.EmbedAsync(texts.Select(t => _o.DocumentPrefix + t).ToList(), ct);
                for (var i = 0; i < batch.Length; i++)
                {
                    batch[i].Embedding = new Vector(vectors[i]);
                    batch[i].EmbeddingHash = EmbeddingText.Hash(texts[i], provider.Model);
                }
                embedded += batch.Length;
            }
            catch (EmbeddingUnavailableException ex)
            {
                failed += batch.Length;
                logger.LogWarning("Job embeddings paused: {Message}", ex.Message);
                break; // the provider is down or misconfigured: do not retry every batch
            }
        }

        await db.SaveChangesAsync(ct);
        if (embedded > 0) logger.LogInformation("Embedded {Count} offers with {Model}.", embedded, provider.Model);
        return new EmbedJobsResult(embedded, failed, Math.Max(0, pending.Count - embedded));
    }

    /// <summary>
    /// Keeps the candidate's vector in sync with their profile. Cheap when nothing changed (a hash comparison); bounded by a
    /// short timeout when it does, because it runs inside the request that saved the profile.
    /// </summary>
    /// <returns>True when the profile has a valid, current embedding after the call.</returns>
    public async Task<bool> EnsureProfileEmbeddingAsync(CandidateProfile profile, JobPreferences prefs, CancellationToken ct = default)
    {
        if (!provider.IsEnabled) return false;

        var text = EmbeddingText.ForProfile(profile, prefs);
        if (!EmbeddingText.IsMeaningful(text))
        {
            // Nothing to say about this candidate yet: a stale vector would mislead, so drop it.
            if (profile.ProfileEmbedding is not null)
            {
                profile.ProfileEmbedding = null;
                profile.EmbeddingHash = null;
                await db.SaveChangesAsync(ct);
            }
            return false;
        }

        var hash = EmbeddingText.Hash(text, provider.Model);
        if (profile.ProfileEmbedding is not null && profile.EmbeddingHash == hash) return true;

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _o.InteractiveTimeoutSeconds)));
        try
        {
            var vectors = await provider.EmbedAsync([_o.QueryPrefix + text], budget.Token);
            profile.ProfileEmbedding = new Vector(vectors[0]);
            profile.EmbeddingHash = hash;
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is EmbeddingUnavailableException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogInformation("Profile embedding skipped: {Message}", ex.Message);
            return profile.ProfileEmbedding is not null && profile.EmbeddingHash == hash;
        }
    }

    /// <summary>Catch-up for profiles saved while the embedding server was unavailable (used by the worker).</summary>
    public async Task<int> EmbedStaleProfilesAsync(int max = 100, CancellationToken ct = default)
    {
        if (!provider.IsEnabled) return 0;

        var profiles = await db.CandidateProfiles.Where(p => p.ProfileEmbedding == null).Take(max).ToListAsync(ct);
        var done = 0;
        foreach (var profile in profiles)
        {
            var prefs = await db.JobPreferences.SingleOrDefaultAsync(p => p.UserId == profile.UserId, ct);
            if (prefs is not null && await EnsureProfileEmbeddingAsync(profile, prefs, ct)) done++;
        }
        return done;
    }
}
