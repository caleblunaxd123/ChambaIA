using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace ChambaIA.Worker.Jobs;

/// <summary>
/// Proves the worker can reach the database and reports queue-relevant numbers. It is the first scheduled job;
/// ingestion (phase 3), match refresh and notification digests (phase 6) are registered next to it.
/// </summary>
public sealed class HeartbeatJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<HeartbeatJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ct = context.CancellationToken;
        var now = clock.GetUtcNow();

        var activeJobs = await db.JobOffers.CountAsync(j => j.IsActive && (j.ExpiresAt == null || j.ExpiresAt > now), ct);
        var candidates = await db.CandidateProfiles.CountAsync(ct);

        logger.LogInformation("Worker heartbeat: {ActiveJobs} active offers, {Candidates} candidate profiles.", activeJobs, candidates);
    }
}
