using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Infrastructure.Tracking;

/// <summary>
/// Keeps the two views of "what I did with this job" consistent: the match status (feed reaction)
/// and the application card (tracker pipeline).
/// </summary>
public sealed class TrackerService(AppDbContext db, TimeProvider clock)
{
    public async Task<CandidateJobMatch?> MarkSeenAsync(Guid userId, Guid jobId, CancellationToken ct)
    {
        var match = await FindMatchAsync(userId, jobId, ct);
        if (match is { Status: MatchStatus.New })
        {
            match.Status = MatchStatus.Seen;
            match.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
        return match;
    }

    public async Task<CandidateJobMatch?> MarkInterestedAsync(Guid userId, Guid jobId, CancellationToken ct)
    {
        var match = await FindMatchAsync(userId, jobId, ct);
        if (match is null) return null;

        var application = await db.Applications.SingleOrDefaultAsync(a => a.UserId == userId && a.JobId == jobId, ct);
        if (application is null)
            db.Applications.Add(new JobApplication { UserId = userId, JobId = jobId, Status = ApplicationStatus.Interested });
        else if (application.Status is ApplicationStatus.Found or ApplicationStatus.Discarded)
        {
            application.Status = ApplicationStatus.Interested;
            application.UpdatedAt = clock.GetUtcNow();
        }

        // Never downgrade a job the user already applied to.
        if (match.Status is not (MatchStatus.Applied or MatchStatus.Interview or MatchStatus.Offer))
            match.Status = MatchStatus.Interested;
        match.UpdatedAt = clock.GetUtcNow();

        await db.SaveChangesAsync(ct);
        return match;
    }

    public async Task<CandidateJobMatch?> DismissAsync(Guid userId, Guid jobId, CancellationToken ct)
    {
        var match = await FindMatchAsync(userId, jobId, ct);
        if (match is null) return null;

        match.Status = MatchStatus.Dismissed;
        match.UpdatedAt = clock.GetUtcNow();

        var application = await db.Applications.SingleOrDefaultAsync(a => a.UserId == userId && a.JobId == jobId, ct);
        if (application is { Status: ApplicationStatus.Found or ApplicationStatus.Interested })
        {
            application.Status = ApplicationStatus.Discarded;
            application.UpdatedAt = clock.GetUtcNow();
        }

        await db.SaveChangesAsync(ct);
        return match;
    }

    /// <summary>Applies a tracker status change and mirrors it on the match.</summary>
    public async Task ApplyStatusAsync(JobApplication application, ApplicationStatus status, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        application.Status = status;
        application.UpdatedAt = now;
        if (status == ApplicationStatus.Applied) application.AppliedAt ??= now;

        var match = await FindMatchAsync(application.UserId, application.JobId, ct);
        if (match is not null)
        {
            match.Status = status switch
            {
                ApplicationStatus.Interested => MatchStatus.Interested,
                ApplicationStatus.Applied => MatchStatus.Applied,
                ApplicationStatus.Interview => MatchStatus.Interview,
                ApplicationStatus.Offer => MatchStatus.Offer,
                ApplicationStatus.Discarded => MatchStatus.Dismissed,
                _ => match.Status
            };
            match.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<CandidateJobMatch?> FindMatchAsync(Guid userId, Guid jobId, CancellationToken ct) =>
        await db.Matches
            .Where(m => m.JobId == jobId && db.CandidateProfiles.Any(p => p.Id == m.CandidateId && p.UserId == userId))
            .SingleOrDefaultAsync(ct);
}
