using ChambaIA.Infrastructure.Ingestion;
using Quartz;

namespace ChambaIA.Worker.Jobs;

/// <summary>
/// Periodic ingestion: every configured source → normalise → dedup → store → retire stale → rematch everyone.
/// Idempotent and non-concurrent, so a slow run is simply followed by the next one.
/// </summary>
[DisallowConcurrentExecution]
public sealed class IngestionJob(IServiceScopeFactory scopes) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IngestionService>().RunAsync(context.CancellationToken);
    }
}
