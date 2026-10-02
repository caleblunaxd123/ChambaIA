using ChambaIA.Api.Contracts;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Domain.Entities;
using ChambaIA.Infrastructure.Notifications;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Api.Endpoints;

public static class NotificationEndpoints
{
    /// <summary>A person has a phone, maybe a tablet and an old phone: more than this is a stale list, not devices.</summary>
    private const int MaxActiveDevices = 5;

    public static RouteGroupBuilder MapNotifications(this RouteGroupBuilder api)
    {
        var devices = api.MapGroup("/devices").WithTags("Notifications").RequireAuthorization();

        // Idempotent. A token is bound to one user: if the same phone signs in with another account the token moves with it,
        // so the previous account stops receiving that person's alerts on this phone.
        devices.MapPost("", async (RegisterDeviceRequest request, HttpContext http, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
            {
                var userId = http.User.GetUserId();
                var now = clock.GetUtcNow();

                var device = await db.DeviceTokens.SingleOrDefaultAsync(d => d.Token == request.Token, ct);
                if (device is null)
                {
                    device = new DeviceToken { Token = request.Token, CreatedAt = now };
                    db.DeviceTokens.Add(device);
                }
                device.UserId = userId;
                device.Platform = request.Platform;
                device.LastSeenAt = now;
                device.DisabledAt = null;
                await db.SaveChangesAsync(ct);

                var surplus = await db.DeviceTokens
                    .Where(d => d.UserId == userId && d.DisabledAt == null)
                    .OrderByDescending(d => d.LastSeenAt)
                    .Skip(MaxActiveDevices)
                    .ToListAsync(ct);
                foreach (var old in surplus) old.DisabledAt = now;
                if (surplus.Count > 0) await db.SaveChangesAsync(ct);

                return Results.NoContent();
            })
            .Validate<RegisterDeviceRequest>();

        // POST instead of DELETE-with-body: some proxies and clients drop bodies on DELETE.
        devices.MapPost("/unregister", async (UnregisterDeviceRequest request, HttpContext http, AppDbContext db, CancellationToken ct) =>
            {
                var userId = http.User.GetUserId();
                await db.DeviceTokens.Where(d => d.Token == request.Token && d.UserId == userId).ExecuteDeleteAsync(ct);
                return Results.NoContent();
            })
            .Validate<UnregisterDeviceRequest>();

        var inbox = api.MapGroup("/notifications").WithTags("Notifications").RequireAuthorization();

        inbox.MapGet("", async (int? page, int? pageSize, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var (p, size) = (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? 20, 1, 50));

            var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(n => n.CreatedAt).Skip((p - 1) * size).Take(size).ToListAsync(ct);

            return Results.Ok(new PagedResult<NotificationDto>(
                items.Select(n => new NotificationDto(n.Id, n.Kind, n.Title, n.Body, n.JobId, n.MatchCount, n.StrongCount, n.CreatedAt, n.ReadAt)).ToList(),
                p, size, total, p * size < total));
        });

        inbox.MapGet("/summary", async (HttpContext http, AppDbContext db, IPushSender push, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            return Results.Ok(new NotificationSummaryDto(
                await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct),
                await db.DeviceTokens.CountAsync(d => d.UserId == userId && d.DisabledAt == null, ct),
                push.IsEnabled));
        });

        inbox.MapPost("/read-all", async (HttpContext http, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var now = clock.GetUtcNow();
            await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
            return Results.NoContent();
        });

        inbox.MapPost("/{id:guid}/read", async (Guid id, HttpContext http, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var now = clock.GetUtcNow();
            var changed = await db.Notifications.Where(n => n.Id == id && n.UserId == userId && n.ReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
            var exists = changed > 0 || await db.Notifications.AnyAsync(n => n.Id == id && n.UserId == userId, ct);
            return exists ? Results.NoContent() : Results.NotFound();
        });

        // "Is this phone getting my alerts?" Sends a harmless notice; at most a few per hour.
        inbox.MapPost("/test", async (HttpContext http, DigestService digest, CancellationToken ct) =>
        {
            var result = await digest.SendTestAsync(http.User.GetUserId(), ct);
            return result is null
                ? Results.Problem("Ya enviaste varias pruebas. Inténtalo de nuevo en un rato.", statusCode: StatusCodes.Status429TooManyRequests, title: "Demasiadas pruebas")
                : Results.Ok(new TestNotificationDto(result.Devices, result.Accepted, result.PushConfigured));
        });

        return api;
    }
}
