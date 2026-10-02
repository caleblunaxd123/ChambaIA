using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChambaIA.Infrastructure.Notifications;

public sealed class PushOptions
{
    public const string Section = "Push";

    /// <summary>"None" (default: notifications are recorded in the inbox only) or "Expo".</summary>
    public string Provider { get; set; } = "None";
    public string BaseUrl { get; set; } = "https://exp.host";
    /// <summary>Optional Expo access token ("enhanced push security"). Environment variable only, never committed.</summary>
    public string? AccessToken { get; set; }
    public int BatchSize { get; set; } = 100;

    // Anti-bombardment rules (see NotificationPolicy).
    public int UtcOffsetHours { get; set; } = -5;
    public int QuietStartHour { get; set; } = 22;
    public int QuietEndHour { get; set; } = 7;
    public int DailyDigestHour { get; set; } = 8;
    public int MaxPerDay { get; set; } = 4;
    public int InstantMinGapMinutes { get; set; } = 30;

    public bool Enabled => string.Equals(Provider, "Expo", StringComparison.OrdinalIgnoreCase);
}

public sealed record PushMessage(string To, string Title, string Body, IReadOnlyDictionary<string, object?> Data);

/// <summary>What happened to a batch: how many were accepted by the push service and which tokens must never be used again.</summary>
public sealed record PushResult(int Accepted, int Failed, IReadOnlyList<string> InvalidTokens)
{
    public static readonly PushResult None = new(0, 0, []);
}

public interface IPushSender
{
    bool IsEnabled { get; }
    Task<PushResult> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct);
}

/// <summary>Push is optional: with no provider the inbox still works and nothing leaves the server.</summary>
public sealed class NullPushSender : IPushSender
{
    public bool IsEnabled => false;
    public Task<PushResult> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct) => Task.FromResult(PushResult.None);
}

/// <summary>
/// Expo Push API (<c>POST /--/api/v2/push/send</c>). Sends in batches of up to 100, retries a transient failure once, and turns
/// "DeviceNotRegistered" tickets into a list of dead tokens. A push service outage never throws: the notification is already in
/// the user's inbox, so the worst case is a missed buzz.
/// </summary>
public sealed class ExpoPushSender(HttpClient http, IOptions<PushOptions> options, ILogger<ExpoPushSender> logger) : IPushSender
{
    private readonly PushOptions _o = options.Value;

    public bool IsEnabled => true;

    public async Task<PushResult> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct)
    {
        int accepted = 0, failed = 0;
        var invalid = new List<string>();

        foreach (var batch in messages.Chunk(Math.Clamp(_o.BatchSize, 1, 100)))
        {
            var tickets = await SendBatchAsync(batch, ct);
            if (tickets is null)
            {
                failed += batch.Length;
                continue;
            }

            for (var i = 0; i < batch.Length; i++)
            {
                var ticket = i < tickets.Count ? tickets[i] : null;
                if (ticket?.Status == "ok") accepted++;
                else
                {
                    failed++;
                    if (ticket?.Details?.Error == "DeviceNotRegistered") invalid.Add(batch[i].To);
                    else logger.LogWarning("Expo rejected a push: {Error}", ticket?.Message ?? ticket?.Details?.Error ?? "unknown");
                }
            }
        }

        return new PushResult(accepted, failed, invalid);
    }

    private async Task<List<Ticket>?> SendBatchAsync(PushMessage[] batch, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "--/api/v2/push/send")
                {
                    Content = JsonContent.Create(batch.Select(m => new ExpoMessage(m.To, m.Title, m.Body, m.Data)).ToList())
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(_o.AccessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _o.AccessToken);

                using var response = await http.SendAsync(request, ct);
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                    throw new HttpRequestException($"Expo answered {(int)response.StatusCode}.");
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Expo rejected the push request: {Status}", (int)response.StatusCode);
                    return null;
                }

                var body = await response.Content.ReadFromJsonAsync<ExpoResponse>(ct);
                return body?.Data ?? [];
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
            {
                if (attempt == 2)
                {
                    logger.LogWarning(ex, "Expo push request failed; the notifications stay in the inbox.");
                    return null;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(800), ct);
            }
        }
        return null;
    }

    private sealed record ExpoMessage(
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("data")] IReadOnlyDictionary<string, object?> Data)
    {
        [JsonPropertyName("sound")] public string Sound => "default";
        [JsonPropertyName("channelId")] public string ChannelId => "default";
        [JsonPropertyName("priority")] public string Priority => "high";
    }

    private sealed record ExpoResponse([property: JsonPropertyName("data")] List<Ticket>? Data);

    private sealed record Ticket(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("details")] TicketDetails? Details);

    private sealed record TicketDetails([property: JsonPropertyName("error")] string? Error);
}
