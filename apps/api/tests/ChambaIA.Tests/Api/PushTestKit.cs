using ChambaIA.Infrastructure.Notifications;

namespace ChambaIA.Tests.Api;

/// <summary>Records what would be pushed; tokens in <see cref="Dead"/> come back as unregistered, like Expo does.</summary>
internal sealed class FakePush : IPushSender
{
    public bool IsEnabled { get; set; } = true;
    public HashSet<string> Dead { get; } = [];
    public List<PushMessage> Sent { get; } = [];

    public Task<PushResult> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct)
    {
        Sent.AddRange(messages);
        var dead = messages.Where(m => Dead.Contains(m.To)).Select(m => m.To).ToList();
        return Task.FromResult(new PushResult(messages.Count - dead.Count, dead.Count, dead));
    }

    public List<PushMessage> To(string token) => Sent.Where(m => m.To == token).ToList();
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal static class PushTestKit
{
    public static string NewToken() => $"ExponentPushToken[{Guid.NewGuid():N}]";

    /// <summary>A moment in the future (so every real timestamp is "before" it) at the given Lima wall-clock hour. Always UTC, like the real clock.</summary>
    public static DateTimeOffset LimaAt(int hour, int minute = 0, int daysAhead = 2)
    {
        var day = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).Date.AddDays(daysAhead);
        return new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, TimeSpan.FromHours(-5)).ToUniversalTime();
    }
}
