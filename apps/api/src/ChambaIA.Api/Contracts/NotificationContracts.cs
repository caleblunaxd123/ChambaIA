using System.ComponentModel.DataAnnotations;
using ChambaIA.Domain.Entities;

namespace ChambaIA.Api.Contracts;

public sealed class RegisterDeviceRequest
{
    /// <summary>The Expo push token of this installation, e.g. "ExponentPushToken[xxxxxxxxxxxxxxxxxxxxxx]".</summary>
    [Required, RegularExpression(@"^Expo(nent)?PushToken\[[A-Za-z0-9_\-]{10,120}\]$", ErrorMessage = "El token de notificaciones no es válido.")]
    public string Token { get; init; } = "";

    [Required, RegularExpression("^(ios|android)$", ErrorMessage = "Plataforma no válida.")]
    public string Platform { get; init; } = "";
}

public sealed class UnregisterDeviceRequest
{
    [Required, StringLength(200)]
    public string Token { get; init; } = "";
}

public sealed record NotificationDto(
    Guid Id,
    NotificationKind Kind,
    string Title,
    string Body,
    Guid? JobId,
    int MatchCount,
    int StrongCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record NotificationSummaryDto(int Unread, int ActiveDevices, bool PushConfigured);

public sealed record TestNotificationDto(int Devices, int Accepted, bool PushConfigured);
