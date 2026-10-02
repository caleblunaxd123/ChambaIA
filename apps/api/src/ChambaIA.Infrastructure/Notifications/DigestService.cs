using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Notifications;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChambaIA.Infrastructure.Notifications;

public sealed record DigestReport(int UsersChecked, int Notified, int PushesAccepted, int TokensDisabled);

public sealed record TestNotificationResult(Guid NotificationId, int Devices, int Accepted, bool PushConfigured);

/// <summary>
/// The agent's voice. For every user who wants notifications it looks at what appeared since the last notice, asks
/// <see cref="NotificationPolicy"/> whether to speak (quiet hours, daily cap, frequency, "only if it is really good") and, if so,
/// writes the notice to the inbox and pushes it to the user's devices. Idempotent: the inbox row is the memory, so running it twice
/// in a row, or after a crash, never produces a duplicate.
/// </summary>
public sealed class DigestService(
    AppDbContext db,
    IPushSender push,
    IOptions<PushOptions> options,
    TimeProvider clock,
    ILogger<DigestService> logger)
{
    private const int MaxTestsPerHour = 3;

    public async Task<DigestReport> RunAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var policy = PolicyOptions();

        var recipients = await db.JobPreferences.AsNoTracking()
            .Where(p => p.PushEnabled)
            .Join(db.CandidateProfiles.Where(c => c.OnboardingCompletedAt != null), p => p.UserId, c => c.UserId,
                (p, c) => new { p.UserId, p.NotificationFrequency, ProfileId = c.Id, Since = c.OnboardingCompletedAt!.Value })
            .ToListAsync(ct);

        int notified = 0, accepted = 0, disabled = 0;
        foreach (var r in recipients)
        {
            ct.ThrowIfCancellationRequested();
            var outcome = await NotifyUserAsync(r.UserId, r.ProfileId, r.NotificationFrequency, r.Since, now, policy, ct);
            if (outcome is null) continue;
            notified++;
            accepted += outcome.Accepted;
            disabled += outcome.Disabled;
        }

        if (notified > 0)
            logger.LogInformation("Digest: {Notified}/{Checked} users notified, {Accepted} pushes accepted, {Disabled} dead tokens disabled.",
                notified, recipients.Count, accepted, disabled);
        return new DigestReport(recipients.Count, notified, accepted, disabled);
    }

    private sealed record UserOutcome(int Accepted, int Disabled);

    private async Task<UserOutcome?> NotifyUserAsync(
        Guid userId, Guid profileId, NotificationFrequency frequency, DateTimeOffset since, DateTimeOffset now, PushPolicyOptions policy, CancellationToken ct)
    {
        var history = db.Notifications.AsNoTracking().Where(n => n.UserId == userId && n.Kind == NotificationKind.NewJobs);
        var last = await history.MaxAsync(n => (DateTimeOffset?)n.CreatedAt, ct);
        var sentToday = await history.CountAsync(n => n.CreatedAt > now.AddHours(-24), ct);

        // "New" means: the offer itself first appeared after the last notice (or after the user finished onboarding) and they
        // have not opened it. The batch they saw while setting up their profile is therefore never announced as news.
        var cutoff = last ?? since;
        var candidates = await db.Matches.AsNoTracking()
            .Where(m => m.CandidateId == profileId && m.Status == MatchStatus.New && m.Category != MatchCategory.Poor)
            .Where(m => m.Job!.IsActive && m.Job.FirstSeenAt > cutoff)
            .Select(m => new NotifiableMatch(m.JobId, m.Job!.Title, m.Job.Company, m.Job.District, m.Job.PostedAt, m.Category, m.OverallScore))
            .ToListAsync(ct);
        if (candidates.Count == 0) return null;

        var decision = NotificationPolicy.Decide(now, frequency, last, sentToday, candidates, policy);
        if (!decision.ShouldSend) return null;

        var message = NotificationCopy.Compose(candidates, now);
        var entry = new NotificationLog
        {
            UserId = userId,
            Kind = NotificationKind.NewJobs,
            Title = message.Title,
            Body = message.Body,
            JobId = message.JobId,
            MatchCount = decision.TotalCount,
            StrongCount = decision.StrongCount,
            CreatedAt = now
        };
        db.Notifications.Add(entry);
        await db.SaveChangesAsync(ct); // the memory first: whatever happens next, this notice is never sent twice

        var (accepted, disabled) = await PushAsync(userId, entry, "new-jobs", ct);
        return new UserOutcome(accepted, disabled);
    }

    /// <summary>Sends a harmless notice so the user can verify that alerts reach this phone. Limited to a few per hour.</summary>
    public async Task<TestNotificationResult?> SendTestAsync(Guid userId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var recent = await db.Notifications.CountAsync(n => n.UserId == userId && n.Kind == NotificationKind.Test && n.CreatedAt > now.AddHours(-1), ct);
        if (recent >= MaxTestsPerHour) return null;

        var message = NotificationCopy.Test();
        var entry = new NotificationLog { UserId = userId, Kind = NotificationKind.Test, Title = message.Title, Body = message.Body, CreatedAt = now };
        db.Notifications.Add(entry);
        await db.SaveChangesAsync(ct);

        var devices = await db.DeviceTokens.CountAsync(d => d.UserId == userId && d.DisabledAt == null, ct);
        var (accepted, _) = await PushAsync(userId, entry, "test", ct);
        return new TestNotificationResult(entry.Id, devices, accepted, push.IsEnabled);
    }

    private async Task<(int Accepted, int Disabled)> PushAsync(Guid userId, NotificationLog entry, string type, CancellationToken ct)
    {
        if (!push.IsEnabled) return (0, 0);

        var tokens = await db.DeviceTokens.Where(d => d.UserId == userId && d.DisabledAt == null).ToListAsync(ct);
        if (tokens.Count == 0) return (0, 0);

        var data = new Dictionary<string, object?> { ["type"] = type, ["notificationId"] = entry.Id, ["jobId"] = entry.JobId, ["tab"] = "new" };
        var result = await push.SendAsync(tokens.Select(t => new PushMessage(t.Token, entry.Title, entry.Body, data)).ToList(), ct);

        entry.DevicesReached = result.Accepted;
        var now = clock.GetUtcNow();
        foreach (var token in tokens.Where(t => result.InvalidTokens.Contains(t.Token))) token.DisabledAt = now;
        await db.SaveChangesAsync(ct);
        return (result.Accepted, result.InvalidTokens.Count);
    }

    private PushPolicyOptions PolicyOptions()
    {
        var o = options.Value;
        return new PushPolicyOptions(o.UtcOffsetHours, o.QuietStartHour, o.QuietEndHour, o.DailyDigestHour, o.MaxPerDay, o.InstantMinGapMinutes);
    }
}
