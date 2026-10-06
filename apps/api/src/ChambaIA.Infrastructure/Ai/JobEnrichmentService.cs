using ChambaIA.Domain.Ai;
using ChambaIA.Domain.Enums;
using ChambaIA.Infrastructure.Matching;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChambaIA.Infrastructure.Ai;

public sealed record EnrichmentReport(int Examined, int NoNeed, int Enriched, int Unreadable, bool Stopped, int UsersRematched)
{
    public static readonly EnrichmentReport None = new(0, 0, 0, 0, false, 0);
}

/// <summary>
/// "Cheap AI, only when it is needed": reads the offers whose gaps the deterministic parser could not fill (salary, experience,
/// skills) and asks the router to fill the blanks. Everything is bounded: a deterministic pre-check decides whether a model is
/// even worth calling, each offer is looked at once per version of its content, a batch has a ceiling, and the budget stops the
/// run the moment it is reached. If AI is off or down it does nothing, and offers keep working with what the parser found.
/// </summary>
public sealed class JobEnrichmentService(
    AppDbContext db,
    AiRouter router,
    MatchRecomputeService matcher,
    IOptions<AiOptions> options,
    ILogger<JobEnrichmentService> logger)
{
    public async Task<EnrichmentReport> RunAsync(CancellationToken ct = default)
    {
        if (!router.CanRoute(AiTask.JobExtraction)) return EnrichmentReport.None;

        var batch = Math.Max(1, options.Value.EnrichmentBatchSize);
        // "Examined" is remembered per content version: nothing is looked at twice, and a changed offer is looked at again.
        var pending = await db.JobOffers
            .Where(j => j.IsActive && j.DuplicateOfId == null && (j.AiExtractionHash == null || j.AiExtractionHash != j.ContentHash))
            .OrderByDescending(j => j.PostedAt)
            .Take(batch * 5)
            .ToListAsync(ct);

        int calls = 0, noNeed = 0, enriched = 0, unreadable = 0;
        var stopped = false;
        var changed = new List<Guid>();

        foreach (var job in pending)
        {
            ct.ThrowIfCancellationRequested();
            if (calls >= batch) break;

            if (!JobAmbiguity.NeedsExtraction(job))
            {
                job.AiExtractionHash = job.ContentHash; // nothing to ask: remember it so the queue keeps moving
                noNeed++;
                continue;
            }

            var (system, user) = JobExtractionPrompt.Build(job);
            var result = await router.CompleteAsync(AiTask.JobExtraction, new AiCall(null, SubscriptionPlan.Free, system, user, MaxOutputTokens: 400), ct);
            if (!result.Ok)
            {
                stopped = true; // budget reached or providers down: stop now, resume on the next run
                break;
            }
            calls++;

            var parsed = JobExtractionParser.Parse(result.Text);
            var extraction = parsed is null ? null : JobExtractionGrounding.Ground(parsed, job.Description); // keep only what the text itself backs up
            job.AiExtractionHash = job.ContentHash;
            if (extraction is null)
            {
                unreadable++;
                continue;
            }

            if (JobExtractionMerger.Apply(job, extraction).Count > 0)
            {
                enriched++;
                changed.Add(job.Id);
            }
        }

        await db.SaveChangesAsync(ct);

        var users = changed.Count > 0 ? await matcher.RecomputeForJobsAsync(changed, ct) : 0;
        if (calls + noNeed > 0)
            logger.LogInformation("Offer enrichment: {Calls} model calls, {NoNeed} offers needed nothing, {Enriched} enriched, {Unreadable} unreadable answers{Stopped}; {Users} users rematched.",
                calls, noNeed, enriched, unreadable, stopped ? " (stopped early: budget or providers)" : "", users);
        return new EnrichmentReport(calls + noNeed, noNeed, enriched, unreadable, stopped, users);
    }
}
