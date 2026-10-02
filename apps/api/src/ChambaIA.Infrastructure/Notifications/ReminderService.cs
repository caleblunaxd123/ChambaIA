using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Notifications;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChambaIA.Infrastructure.Notifications;

public sealed record ReminderReport(int CardsChecked, int Sent, int PushesAccepted);

/// <summary>
/// Reminders about the user's own pipeline: the day before and shortly before an interview, and one gentle "any news?" for an
/// application that has been silent for a week. <see cref="ReminderPolicy"/> decides; like the digest, the inbox row is written
/// first so a crash or a second run can never repeat a reminder.
/// </summary>
public sealed class ReminderService(
    AppDbContext db,
    IPushSender push,
    IOptions<PushOptions> options,
    TimeProvider clock,
    ILogger<ReminderService> logger)
{
    private static readonly NotificationKind[] ReminderKinds = [NotificationKind.InterviewDayBefore, NotificationKind.InterviewSoon, NotificationKind.FollowUp];

    public async Task<ReminderReport> RunAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var o = options.Value;
        var policy = new PushPolicyOptions(o.UtcOffsetHours, o.QuietStartHour, o.QuietEndHour, o.DailyDigestHour, o.MaxPerDay, o.InstantMinGapMinutes);
        var rules = new ReminderRules();
        var offset = TimeSpan.FromHours(o.UtcOffsetHours);

        // Only the cards that can possibly be due: an interview in the next 24 h, or an application silent for a week.
        var horizon = now.AddHours(rules.DayBeforeFromHours);
        var silentSince = now.AddDays(-rules.FollowUpAfterDays);
        var rows = await db.Applications.AsNoTracking()
            .Where(a => db.JobPreferences.Any(p => p.UserId == a.UserId && p.PushEnabled))
            .Where(a => a.Status != ApplicationStatus.Discarded && a.Status != ApplicationStatus.Found)
            .Where(a => (a.InterviewDate != null && a.InterviewDate > now && a.InterviewDate <= horizon)
                     || (a.InterviewDate == null && a.Status == ApplicationStatus.Applied && a.AppliedAt != null && a.AppliedAt <= silentSince && a.UpdatedAt <= silentSince))
            .Select(a => new { a.UserId, a.Id, a.JobId, a.Job!.Title, a.Job.Company, a.Status, a.InterviewDate, a.AppliedAt, a.UpdatedAt })
            .ToListAsync(ct);
        if (rows.Count == 0) return new ReminderReport(0, 0, 0);

        var ids = rows.Select(r => r.Id).ToList();
        var history = (await db.Notifications.AsNoTracking()
                .Where(n => n.ApplicationId != null && ids.Contains(n.ApplicationId.Value) && ReminderKinds.Contains(n.Kind))
                .Select(n => new { n.ApplicationId, n.Kind, n.RemindFor })
                .ToListAsync(ct))
            .ToLookup(n => n.ApplicationId!.Value, n => new SentReminder(n.Kind, n.RemindFor));

        // "Any news?" is a nudge, not an event: at most one per user per day.
        var users = rows.Select(r => r.UserId).Distinct().ToList();
        var nudgedToday = (await db.Notifications.AsNoTracking()
                .Where(n => n.Kind == NotificationKind.FollowUp && n.CreatedAt > now.AddHours(-24) && users.Contains(n.UserId))
                .Select(n => n.UserId)
                .ToListAsync(ct))
            .ToHashSet();

        int sent = 0, accepted = 0;
        foreach (var row in rows.OrderBy(r => r.InterviewDate ?? DateTimeOffset.MaxValue).ThenBy(r => r.AppliedAt))
        {
            ct.ThrowIfCancellationRequested();
            var card = new ReminderCandidate(row.Id, row.JobId, row.Title, row.Company, row.Status, row.InterviewDate, row.AppliedAt, row.UpdatedAt);
            var due = ReminderPolicy.Due(now, card, history[row.Id].ToList(), policy, rules);
            if (due is null) continue;
            if (due.Kind == NotificationKind.FollowUp && !nudgedToday.Add(row.UserId)) continue;

            var message = NotificationCopy.Reminder(due.Kind, card, due.For, now, offset);
            var entry = new NotificationLog
            {
                UserId = row.UserId, Kind = due.Kind, Title = message.Title, Body = message.Body, JobId = message.JobId,
                ApplicationId = row.Id, RemindFor = due.For, CreatedAt = now
            };
            db.Notifications.Add(entry);
            await db.SaveChangesAsync(ct); // the memory first

            var (ok, _) = await PushDelivery.SendAsync(db, push, clock, row.UserId, entry, due.Kind == NotificationKind.FollowUp ? "follow-up" : "interview", ct);
            sent++;
            accepted += ok;
        }

        if (sent > 0) logger.LogInformation("Reminders: {Sent} sent from {Cards} cards, {Accepted} pushes accepted.", sent, rows.Count, accepted);
        return new ReminderReport(rows.Count, sent, accepted);
    }
}
