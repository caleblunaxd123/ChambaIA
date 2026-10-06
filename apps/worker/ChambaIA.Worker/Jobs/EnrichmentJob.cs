using ChambaIA.Infrastructure.Ai;
using Quartz;

namespace ChambaIA.Worker.Jobs;

/// <summary>
/// Sends the offers the deterministic parser could not fully read to a cheap model, within the budget. Non-concurrent, and a
/// complete no-op when AI is off: the queue simply waits until someone turns it on.
/// </summary>
[DisallowConcurrentExecution]
public sealed class EnrichmentJob(IServiceScopeFactory scopes) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<JobEnrichmentService>().RunAsync(context.CancellationToken);
    }
}
