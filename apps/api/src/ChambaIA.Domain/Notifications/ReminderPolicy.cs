using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Notifications;

/// <summary>What the reminder rules need to know about one tracker card.</summary>
public sealed record ReminderCandidate(
    Guid ApplicationId,
    Guid JobId,
    string Title,
    string Company,
    ApplicationStatus Status,
    DateTimeOffset? InterviewDate,
    DateTimeOffset? AppliedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A reminder that was already sent for this card (and, for interviews, for which date: a rescheduled interview reminds again).</summary>
public sealed record SentReminder(NotificationKind Kind, DateTimeOffset? For);

public sealed record DueReminder(NotificationKind Kind, DateTimeOffset? For);

public sealed record ReminderRules(
    int DayBeforeFromHours = 24,   // the "day before" reminder opens 24 h ahead...
    int DayBeforeUntilHours = 6,   // ...and stops 6 h ahead (closer than that, only the "soon" one makes sense)
    int SoonHours = 2,
    int FollowUpAfterDays = 7,
    int FollowUpGiveUpDays = 30,   // after a month of silence a nudge is noise, not help
    int FollowUpFromHour = 9,
    int FollowUpUntilHour = 20);

/// <summary>
/// Interview reminders and the "any news?" nudge as a pure function of (time, card, history). Reminders are things the user
/// asked for by scheduling an interview, so they are not subject to the new-offers daily cap, but they respect quiet hours.
/// </summary>
public static class ReminderPolicy
{
    public static DueReminder? Due(
        DateTimeOffset now,
        ReminderCandidate card,
        IReadOnlyCollection<SentReminder> sent,
        PushPolicyOptions push,
        ReminderRules? rules = null)
    {
        rules ??= new ReminderRules();
        if (card.Status is ApplicationStatus.Discarded or ApplicationStatus.Found) return null;

        var local = now.ToOffset(TimeSpan.FromHours(push.UtcOffsetHours));
        if (NotificationPolicy.IsQuiet(local.Hour, push)) return null;

        if (card.InterviewDate is { } date)
        {
            var until = date - now;
            if (until <= TimeSpan.Zero) return null;

            if (until <= TimeSpan.FromHours(rules.SoonHours))
                return WasSent(sent, NotificationKind.InterviewSoon, date) ? null : new DueReminder(NotificationKind.InterviewSoon, date);

            if (until <= TimeSpan.FromHours(rules.DayBeforeFromHours) && until > TimeSpan.FromHours(rules.DayBeforeUntilHours))
                return WasSent(sent, NotificationKind.InterviewDayBefore, date) ? null : new DueReminder(NotificationKind.InterviewDayBefore, date);

            return null;
        }

        if (card.Status != ApplicationStatus.Applied || card.AppliedAt is null) return null;
        if (local.Hour < rules.FollowUpFromHour || local.Hour >= rules.FollowUpUntilHour) return null;

        // Silence = neither the application nor the card has moved. Anything the user touched resets the clock.
        var lastActivity = card.AppliedAt.Value > card.UpdatedAt ? card.AppliedAt.Value : card.UpdatedAt;
        var quiet = now - lastActivity;
        if (quiet < TimeSpan.FromDays(rules.FollowUpAfterDays) || quiet > TimeSpan.FromDays(rules.FollowUpGiveUpDays)) return null;

        return sent.Any(s => s.Kind == NotificationKind.FollowUp) ? null : new DueReminder(NotificationKind.FollowUp, null);
    }

    private static bool WasSent(IReadOnlyCollection<SentReminder> sent, NotificationKind kind, DateTimeOffset date) =>
        sent.Any(s => s.Kind == kind && s.For == date);
}
