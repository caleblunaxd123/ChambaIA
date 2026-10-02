using ChambaIA.Infrastructure.Embeddings;
using ChambaIA.Infrastructure.Matching;
using Quartz;

namespace ChambaIA.Worker.Jobs;

/// <summary>
/// Fills in missing or stale vectors (offers and candidate profiles) and rematches when anything changed. Safe to run when
/// embeddings are disabled or the server is down: it does nothing and the deterministic matching keeps serving users.
/// </summary>
[DisallowConcurrentExecution]
public sealed class EmbeddingJob(IServiceScopeFactory scopes, ILogger<EmbeddingJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = scopes.CreateAsyncScope();
        var embeddings = scope.ServiceProvider.GetRequiredService<EmbeddingService>();
        if (!embeddings.IsEnabled) return;

        var ct = context.CancellationToken;
        var jobs = await embeddings.EmbedPendingJobsAsync(ct: ct);
        var profiles = await embeddings.EmbedStaleProfilesAsync(ct: ct);
        if (jobs.Embedded == 0 && profiles == 0) return;

        var users = await scope.ServiceProvider.GetRequiredService<MatchRecomputeService>().RecomputeAllAsync(ct);
        logger.LogInformation("Embeddings caught up: {Jobs} offers, {Profiles} profiles; {Users} users rematched.", jobs.Embedded, profiles, users);
    }
}
