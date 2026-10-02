using ChambaIA.Domain.Entities;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Infrastructure.Notifications;

/// <summary>
/// The last step of every notice, shared by the digest and the reminders: push an already stored inbox entry to the user's
/// active devices, remember how many accepted it and retire the tokens Expo says are dead.
/// </summary>
internal static class PushDelivery
{
    public static async Task<(int Accepted, int Disabled)> SendAsync(
        AppDbContext db, IPushSender push, TimeProvider clock, Guid userId, NotificationLog entry, string type, CancellationToken ct)
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
}
