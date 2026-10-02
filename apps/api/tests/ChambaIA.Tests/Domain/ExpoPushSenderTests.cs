using System.Net;
using System.Text;
using System.Text.Json;
using ChambaIA.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChambaIA.Tests.Domain;

/// <summary>The Expo client against a scripted server: request shape, batching, dead tokens and outages.</summary>
public class ExpoPushSenderTests
{
    private sealed class Handler(Func<HttpRequestMessage, string, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<(string Path, string Body, string? Auth)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var body = await request.Content!.ReadAsStringAsync(ct);
            Requests.Add((request.RequestUri!.AbsolutePath, body, request.Headers.Authorization?.ToString()));
            return respond(request, body, Calls);
        }
    }

    private static HttpResponseMessage Tickets(params object[] tickets) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { data = tickets }), Encoding.UTF8, "application/json") };

    private static object Ok() => new { status = "ok", id = Guid.NewGuid().ToString() };

    private static object Dead() => new { status = "error", message = "not registered", details = new { error = "DeviceNotRegistered" } };

    private static PushMessage Message(string token) => new(token, "Título", "Cuerpo", new Dictionary<string, object?> { ["type"] = "new-jobs", ["jobId"] = Guid.Empty });

    private static ExpoPushSender Build(Handler handler, Action<PushOptions>? tweak = null)
    {
        var options = new PushOptions { Provider = "Expo" };
        tweak?.Invoke(options);
        return new ExpoPushSender(new HttpClient(handler) { BaseAddress = new Uri("https://exp.test/") }, Options.Create(options), NullLogger<ExpoPushSender>.Instance);
    }

    [Fact]
    public async Task Sends_the_documented_payload_with_sound_channel_and_data()
    {
        var handler = new Handler((_, _, _) => Tickets(Ok()));
        var sender = Build(handler, o => o.AccessToken = "expo-secret");

        var result = await sender.SendAsync([Message("ExponentPushToken[abc]")], default);

        Assert.Equal(1, result.Accepted);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/--/api/v2/push/send", request.Path);
        Assert.Equal("Bearer expo-secret", request.Auth);
        using var body = JsonDocument.Parse(request.Body);
        var first = body.RootElement[0];
        Assert.Equal("ExponentPushToken[abc]", first.GetProperty("to").GetString());
        Assert.Equal("Título", first.GetProperty("title").GetString());
        Assert.Equal("default", first.GetProperty("sound").GetString());
        Assert.Equal("default", first.GetProperty("channelId").GetString());
        Assert.Equal("new-jobs", first.GetProperty("data").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Without_an_access_token_no_authorization_header_is_sent()
    {
        var handler = new Handler((_, _, _) => Tickets(Ok()));

        await Build(handler).SendAsync([Message("ExponentPushToken[abc]")], default);

        Assert.Null(handler.Requests[0].Auth);
    }

    [Fact]
    public async Task Large_sends_are_split_in_batches()
    {
        var handler = new Handler((_, body, _) => Tickets(Enumerable.Range(0, JsonDocument.Parse(body).RootElement.GetArrayLength()).Select(_ => Ok()).ToArray()));
        var sender = Build(handler, o => o.BatchSize = 100);

        var result = await sender.SendAsync(Enumerable.Range(0, 250).Select(i => Message($"ExponentPushToken[t{i}]")).ToList(), default);

        Assert.Equal(250, result.Accepted);
        Assert.Equal(3, handler.Calls); // 100 + 100 + 50
    }

    [Fact]
    public async Task Dead_tokens_are_reported_so_they_are_never_used_again()
    {
        var handler = new Handler((_, _, _) => Tickets(Ok(), Dead(), Ok()));

        var result = await Build(handler).SendAsync([Message("ExponentPushToken[good1]"), Message("ExponentPushToken[gone]"), Message("ExponentPushToken[good2]")], default);

        Assert.Equal((2, 1), (result.Accepted, result.Failed));
        Assert.Equal(["ExponentPushToken[gone]"], result.InvalidTokens);
    }

    [Fact]
    public async Task A_transient_server_error_is_retried_once_and_then_succeeds()
    {
        var handler = new Handler((_, _, call) => call == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Tickets(Ok()));

        var result = await Build(handler).SendAsync([Message("ExponentPushToken[abc]")], default);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task A_push_outage_never_throws_it_just_reports_failures()
    {
        var handler = new Handler((_, _, _) => throw new HttpRequestException("expo is down"));

        var result = await Build(handler).SendAsync([Message("ExponentPushToken[a]"), Message("ExponentPushToken[b]")], default);

        Assert.Equal((0, 2), (result.Accepted, result.Failed));
        Assert.Empty(result.InvalidTokens);
    }

    [Fact]
    public async Task A_rejected_request_is_not_retried()
    {
        var handler = new Handler((_, _, _) => new HttpResponseMessage(HttpStatusCode.BadRequest));

        var result = await Build(handler).SendAsync([Message("ExponentPushToken[a]")], default);

        Assert.Equal(1, result.Failed);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task The_null_sender_does_nothing_and_says_it_is_disabled()
    {
        var sender = new NullPushSender();

        Assert.False(sender.IsEnabled);
        Assert.Equal(PushResult.None, await sender.SendAsync([Message("ExponentPushToken[a]")], default));
    }
}
