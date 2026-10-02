using ChambaIA.Infrastructure.Notifications;
using Quartz;

namespace ChambaIA.Worker.Jobs;

/// <summary>
/// The agent's heartbeat towards the user: every few minutes, decide per person whether something is worth telling them
/// (quiet hours, daily cap and frequency are enforced by the policy, not by this schedule). Idempotent and non-concurrent.
/// </summary>
[DisallowConcurrentExecution]
public sealed class NotificationJob(IServiceScopeFactory scopes) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DigestService>().RunAsync(context.CancellationToken);
    }
}
