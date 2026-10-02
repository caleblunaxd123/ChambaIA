using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Notifications;

/// <summary>An offer that appeared after the last notification and that the user has not opened yet.</summary>
public sealed record NotifiableMatch(
    Guid JobId,
    string Title,
    string Company,
    string? District,
    DateTimeOffset? PostedAt,
    MatchCategory Category,
    double Score)
{
    public bool IsStrong => Category >= MatchCategory.VeryCompatible;
}

public sealed record PushPolicyOptions(
    int UtcOffsetHours = -5,   // Lima has no daylight saving time
    int QuietStartHour = 22,
    int QuietEndHour = 7,
    int DailyDigestHour = 8,
    int MaxPerDay = 4,
    int InstantMinGapMinutes = 30);

public enum SendDecisionKind { Send, NothingNew, QuietHours, DailyCapReached, NotDue }

public sealed record SendDecision(SendDecisionKind Kind, int StrongCount = 0, int TotalCount = 0)
{
    public bool ShouldSend => Kind == SendDecisionKind.Send;
}

/// <summary>
/// "Never bombard": a pure function from (time, frequency, history, what is new) to send / do-not-send. Every rule that protects
/// the user lives here so it can be tested exhaustively with plain dates.
/// </summary>
public static class NotificationPolicy
{
    public static SendDecision Decide(
        DateTimeOffset now,
        NotificationFrequency frequency,
        DateTimeOffset? lastSentAt,
        int sentInLast24Hours,
        IReadOnlyList<NotifiableMatch> candidates,
        PushPolicyOptions options)
    {
        var strong = candidates.Count(c => c.IsStrong);
        // Only a very good fit is worth interrupting someone for; the rest waits for them in the app.
        if (strong == 0) return new SendDecision(SendDecisionKind.NothingNew);

        var local = now.ToOffset(TimeSpan.FromHours(options.UtcOffsetHours));
        if (IsQuiet(local.Hour, options)) return new SendDecision(SendDecisionKind.QuietHours);
        if (sentInLast24Hours >= options.MaxPerDay) return new SendDecision(SendDecisionKind.DailyCapReached);
        if (!IsDue(now, local, frequency, lastSentAt, options)) return new SendDecision(SendDecisionKind.NotDue);

        return new SendDecision(SendDecisionKind.Send, strong, candidates.Count);
    }

    /// <summary>Quiet hours wrap around midnight (22:00 → 07:00).</summary>
    public static bool IsQuiet(int localHour, PushPolicyOptions o) =>
        o.QuietStartHour > o.QuietEndHour
            ? localHour >= o.QuietStartHour || localHour < o.QuietEndHour
            : localHour >= o.QuietStartHour && localHour < o.QuietEndHour;

    private static bool IsDue(DateTimeOffset now, DateTimeOffset local, NotificationFrequency frequency, DateTimeOffset? lastSentAt, PushPolicyOptions o)
    {
        if (frequency == NotificationFrequency.Daily)
        {
            // One digest per local day, not before the digest hour.
            if (local.Hour < o.DailyDigestHour) return false;
            return lastSentAt is null || lastSentAt.Value.ToOffset(local.Offset).Date < local.Date;
        }

        var gap = frequency switch
        {
            NotificationFrequency.Instant => TimeSpan.FromMinutes(o.InstantMinGapMinutes),
            NotificationFrequency.Every2Hours => TimeSpan.FromHours(2),
            _ => TimeSpan.FromHours(6)
        };
        return lastSentAt is null || now - lastSentAt.Value >= gap;
    }
}
