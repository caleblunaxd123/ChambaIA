namespace ChambaIA.Domain.Entities;

/// <summary>An Expo push token of one installed app. A token belongs to exactly one user at a time.</summary>
public class DeviceToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    /// <summary>"ExponentPushToken[...]" as issued by Expo for this installation.</summary>
    public string Token { get; set; } = "";
    public string Platform { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Set when Expo reports the token as no longer valid (app uninstalled): it is never used again.</summary>
    public DateTimeOffset? DisabledAt { get; set; }
}

public enum NotificationKind { NewJobs, Test, InterviewDayBefore, InterviewSoon, FollowUp }

/// <summary>
/// Everything the agent told the user. It is the in-app inbox and, at the same time, the memory the anti-spam rules rely on
/// ("when did I last notify, how many today"), so a restart can never cause a duplicate or a burst.
/// </summary>
public class NotificationLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public NotificationKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    /// <summary>The offer to open when the user taps the notification (the best one of the batch).</summary>
    public Guid? JobId { get; set; }
    public int MatchCount { get; set; }
    public int StrongCount { get; set; }
    /// <summary>How many devices the push was accepted for. 0 is normal: no devices, push off, or the app is not installed.</summary>
    public int DevicesReached { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
    /// <summary>For reminders: the tracker card the notice is about.</summary>
    public Guid? ApplicationId { get; set; }
    /// <summary>For interview reminders: the interview date it was sent for, so a rescheduled interview reminds again but never twice.</summary>
    public DateTimeOffset? RemindFor { get; set; }
}
