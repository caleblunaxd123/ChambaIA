using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Ingestion;
using ChambaIA.Infrastructure.Ingestion;
using ChambaIA.Infrastructure.Notifications;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChambaIA.Tests.Api;

/// <summary>Phase 6: device registration, the inbox, and the digest that decides when the agent speaks.</summary>
[Collection(ApiCollection.Name)]
public class NotificationTests(ApiFactory factory)
{
    private sealed class FakeSource(string key, Func<IReadOnlyList<RawJob>> jobs) : IJobSource
    {
        public string Key => key;
        public string Name => $"Fuente {key}";
        public JobSourceKind Kind => JobSourceKind.Feed;
        public string? BaseUrl => null;
        public Task<IReadOnlyList<RawJob>> FetchJobsAsync(DateTimeOffset? since, CancellationToken ct) => Task.FromResult(jobs());
    }

    /// <summary>Records what would be pushed; tokens in <see cref="Dead"/> come back as unregistered, like Expo does.</summary>
    private sealed class FakePush : IPushSender
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

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private static string NewToken() => $"ExponentPushToken[{Guid.NewGuid():N}]";

    /// <summary>A moment in the future (so every real timestamp is "before" it) at the given Lima wall-clock hour.</summary>
    private static DateTimeOffset LimaAt(int hour, int minute = 0, int daysAhead = 2)
    {
        var day = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).Date.AddDays(daysAhead);
        return new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, TimeSpan.FromHours(-5)).ToUniversalTime(); // the real clock is always UTC
    }

    private async Task<(HttpClient Client, Guid UserId, string Token)> OnboardedUserAsync(string frequency = "instant", bool push = true, bool withDevice = true)
    {
        var (client, auth) = await factory.NewUserAsync();
        var profile = await client.PutAsJsonAsync("/api/v1/profile", new
        {
            fullName = "Usuaria Avisos", experienceMonths = 36, educationLevel = "technical", educationStatus = "completed", completeOnboarding = true,
            skills = new[] { new { key = "", name = "Facturación", level = "advanced" }, new { key = "", name = "Excel", level = "intermediate" } }
        });
        profile.EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/v1/preferences", new { preferredRoles = new[] { "Asistente de Facturación" }, notificationFrequency = frequency, pushEnabled = push }))
            .EnsureSuccessStatusCode();

        var token = NewToken();
        if (withDevice) Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/devices", new { token, platform = "android" })).StatusCode);
        return (client, auth.User.Id, token);
    }

    private async Task<T> WithDb<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task Ingest(params RawJob[] jobs)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IngestionService>().RunAsync([new FakeSource(Unique("avisos"), () => jobs)], recompute: true);
    }

    /// <summary>Simulated time travel: says the offers of this company first appeared at the given (future) moment.</summary>
    private Task AppearedAt(string company, DateTimeOffset at) =>
        WithDb(db => db.JobOffers.Where(j => j.Company == company).ExecuteUpdateAsync(u => u.SetProperty(j => j.FirstSeenAt, at)));

    private static RawJob StrongJob(string company, string? id = null) => new()
    {
        ExternalId = id ?? Unique("sj"),
        Title = "Asistente de Facturación",
        Company = company,
        Description = "Emisión de facturas electrónicas y control de cobranzas. Facturación avanzada y Excel intermedio. 2 años de experiencia. Lunes a viernes. S/ 2,000 - 2,400.",
        Url = $"https://empleos.example.com/{Guid.NewGuid():N}",
        Location = "San Miguel, Lima"
    };

    private async Task<DigestReport> Digest(FakePush push, DateTimeOffset now, Action<PushOptions>? tweak = null)
    {
        var options = new PushOptions { Provider = "Expo" };
        tweak?.Invoke(options);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = new DigestService(scope.ServiceProvider.GetRequiredService<AppDbContext>(), push, Options.Create(options), new FixedClock(now), NullLogger<DigestService>.Instance);
        return await service.RunAsync();
    }

    private static async Task<JsonElement> Inbox(HttpClient client, string query = "") =>
        await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications" + query);

    // ---- digest ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_strong_new_offer_lands_in_the_inbox_and_is_pushed_to_the_users_phone()
    {
        var (client, _, token) = await OnboardedUserAsync();
        var company = Unique("Fact");
        await Ingest(StrongJob(company));
        var push = new FakePush();

        await Digest(push, LimaAt(14));

        var sent = Assert.Single(push.To(token));
        Assert.Equal("Nueva oportunidad para ti", sent.Title);
        Assert.Contains(company, sent.Body);
        Assert.Equal("new-jobs", sent.Data!["type"]);
        Assert.Equal("new", sent.Data["tab"]);

        var inbox = await Inbox(client);
        var item = Assert.Single(inbox.GetProperty("items").EnumerateArray());
        Assert.Equal("newJobs", item.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("readAt").ValueKind);
        Assert.NotEqual(Guid.Empty, item.GetProperty("jobId").GetGuid());
        Assert.Equal(1, item.GetProperty("strongCount").GetInt32());
    }

    [Fact]
    public async Task Running_the_digest_again_never_repeats_a_notice()
    {
        var (client, _, token) = await OnboardedUserAsync();
        await Ingest(StrongJob(Unique("Fact")));
        var push = new FakePush();

        await Digest(push, LimaAt(14));
        await Digest(push, LimaAt(14, 5));
        await Digest(push, LimaAt(18));

        Assert.Single(push.To(token));
        Assert.Equal(1, (await Inbox(client)).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Offers_seen_during_onboarding_are_never_announced_as_news()
    {
        var (client, _, token) = await OnboardedUserAsync(); // the seeded offers exist already and match this profile
        var push = new FakePush();

        var report = await Digest(push, LimaAt(14));

        Assert.Empty(push.To(token));
        Assert.Equal(0, (await Inbox(client)).GetProperty("total").GetInt32());
        Assert.True(report.UsersChecked >= 1);
    }

    [Fact]
    public async Task Quiet_hours_hold_the_notice_until_morning()
    {
        var (client, _, token) = await OnboardedUserAsync();
        await Ingest(StrongJob(Unique("Fact")));
        var push = new FakePush();

        await Digest(push, LimaAt(23, 30));
        Assert.Empty(push.To(token));

        await Digest(push, LimaAt(7, 15, daysAhead: 3));
        Assert.Single(push.To(token));
        Assert.Equal(1, (await Inbox(client)).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Users_who_turned_notifications_off_hear_nothing()
    {
        var (client, _, token) = await OnboardedUserAsync(push: false);
        await Ingest(StrongJob(Unique("Fact")));
        var push = new FakePush();

        await Digest(push, LimaAt(14));

        Assert.Empty(push.To(token));
        Assert.Equal(0, (await Inbox(client)).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task The_inbox_is_kept_even_without_a_device_or_a_push_provider()
    {
        var (client, _, _) = await OnboardedUserAsync(withDevice: false);
        await Ingest(StrongJob(Unique("Fact")));
        var off = new FakePush { IsEnabled = false };

        await Digest(off, LimaAt(14));

        Assert.Empty(off.Sent);
        var item = Assert.Single((await Inbox(client)).GetProperty("items").EnumerateArray());
        Assert.True(item.GetProperty("matchCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task The_daily_cap_and_the_instant_gap_stop_a_burst()
    {
        var (_, _, token) = await OnboardedUserAsync();
        var push = new FakePush();
        void OneADay(PushOptions o) => o.MaxPerDay = 1;

        await Ingest(StrongJob(Unique("Fact")));
        await Digest(push, LimaAt(12), OneADay);
        Assert.Single(push.To(token));

        var second = Unique("Fact");
        await Ingest(StrongJob(second));
        await AppearedAt(second, LimaAt(12, 5));
        await Digest(push, LimaAt(12, 10), o => o.MaxPerDay = 4); // only 10 minutes later: the gap blocks it
        Assert.Single(push.To(token));

        await Digest(push, LimaAt(14), OneADay);                   // gap is fine now but the daily cap is reached
        Assert.Single(push.To(token));

        await Digest(push, LimaAt(14), o => o.MaxPerDay = 4);      // with a normal cap it finally goes out
        Assert.Equal(2, push.To(token).Count);

        await AppearedAt(second, DateTimeOffset.UtcNow.AddDays(-30)); // do not leak a "future" offer into other tests
    }

    [Fact]
    public async Task A_dead_device_token_is_disabled_after_the_first_failure_and_not_tried_again()
    {
        var (client, _, token) = await OnboardedUserAsync();
        await Ingest(StrongJob(Unique("Fact")));
        var push = new FakePush { Dead = { token } };

        var report = await Digest(push, LimaAt(14));

        Assert.Equal(1, report.TokensDisabled);
        var summary = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/summary");
        Assert.Equal(0, summary.GetProperty("activeDevices").GetInt32());
        Assert.Equal(1, summary.GetProperty("unread").GetInt32()); // the notice itself is still in the inbox

        await Ingest(StrongJob(Unique("Fact")));
        await Digest(push, LimaAt(20));
        Assert.Single(push.To(token));
    }

    [Fact]
    public async Task Ingestion_recomputes_only_the_new_offers_not_the_whole_feed()
    {
        var (client, userId, _) = await OnboardedUserAsync();
        // Plant a marker on an old match: a full recompute would overwrite it, an incremental one leaves it alone.
        var planted = await WithDb(async db =>
        {
            var match = await db.Matches.Where(m => m.Candidate!.UserId == userId).OrderBy(m => m.JobId).FirstAsync();
            match.OverallScore = 1;
            await db.SaveChangesAsync();
            return match.JobId;
        });

        var company = Unique("Fact");
        await Ingest(StrongJob(company));

        var (markerKept, newMatches) = await WithDb(async db => (
            await db.Matches.Where(m => m.Candidate!.UserId == userId && m.JobId == planted).Select(m => m.OverallScore).SingleAsync() == 1,
            await db.Matches.CountAsync(m => m.Candidate!.UserId == userId && m.Job!.Company == company)));
        Assert.True(markerKept);
        Assert.Equal(1, newMatches);
        Assert.True((await client.GetFromJsonAsync<JsonElement>("/api/v1/matches?pageSize=5&q=" + company)).GetProperty("total").GetInt32() >= 1);
    }

    // ---- inbox -----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Inbox_tracks_unread_and_lets_the_user_mark_notices_read()
    {
        var (client, _, _) = await OnboardedUserAsync();
        for (var i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/notifications/test", null)).StatusCode);

        Assert.Equal(2, (await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/summary")).GetProperty("unread").GetInt32());
        var first = (await Inbox(client)).GetProperty("items")[0].GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/notifications/{first}/read", null)).StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/summary")).GetProperty("unread").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/summary")).GetProperty("unread").GetInt32());
    }

    [Fact]
    public async Task Inbox_is_private_and_paged()
    {
        var (client, _, _) = await OnboardedUserAsync();
        var (other, _) = await factory.NewUserAsync();
        for (var i = 0; i < 3; i++) await client.PostAsync("/api/v1/notifications/test", null);
        var id = (await Inbox(client)).GetProperty("items")[0].GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/notifications/{id}/read", null)).StatusCode);
        Assert.Equal(0, (await Inbox(other)).GetProperty("total").GetInt32());

        var page = await Inbox(client, "?pageSize=2");
        Assert.Equal(2, page.GetProperty("items").GetArrayLength());
        Assert.True(page.GetProperty("hasMore").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/notifications")).StatusCode);
    }

    [Fact]
    public async Task The_test_notice_reports_devices_and_whether_push_is_configured_and_is_rate_limited()
    {
        var (client, _, _) = await OnboardedUserAsync();

        var response = await client.PostAsync("/api/v1/notifications/test", null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("devices").GetInt32());
        Assert.False(body.GetProperty("pushConfigured").GetBoolean()); // the test environment has no Expo provider

        await client.PostAsync("/api/v1/notifications/test", null);
        await client.PostAsync("/api/v1/notifications/test", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/api/v1/notifications/test", null)).StatusCode);
    }

    // ---- devices ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Registering_a_device_is_idempotent_and_unregistering_removes_it()
    {
        var (client, _) = await factory.NewUserAsync();
        var token = NewToken();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/devices", new { token, platform = "ios" })).StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/summary")).GetProperty("activeDevices").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/devices/unregister", new { token })).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/summary")).GetProperty("activeDevices").GetInt32());
    }

    [Fact]
    public async Task A_phone_that_signs_in_with_another_account_moves_its_alerts_with_it()
    {
        var (first, _) = await factory.NewUserAsync();
        var (second, _) = await factory.NewUserAsync();
        var token = NewToken();

        await first.PostAsJsonAsync("/api/v1/devices", new { token, platform = "android" });
        await second.PostAsJsonAsync("/api/v1/devices", new { token, platform = "android" });

        Assert.Equal(0, (await first.GetFromJsonAsync<JsonElement>("/api/v1/notifications/summary")).GetProperty("activeDevices").GetInt32());
        Assert.Equal(1, (await second.GetFromJsonAsync<JsonElement>("/api/v1/notifications/summary")).GetProperty("activeDevices").GetInt32());

        // Someone else cannot unregister a token they do not own.
        await first.PostAsJsonAsync("/api/v1/devices/unregister", new { token });
        Assert.Equal(1, (await second.GetFromJsonAsync<JsonElement>("/api/v1/notifications/summary")).GetProperty("activeDevices").GetInt32());
    }

    [Fact]
    public async Task Only_the_five_most_recent_devices_stay_active()
    {
        var (client, _) = await factory.NewUserAsync();
        for (var i = 0; i < 7; i++)
        {
            await client.PostAsJsonAsync("/api/v1/devices", new { token = NewToken(), platform = "android" });
            await Task.Delay(5);
        }

        Assert.Equal(5, (await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/summary")).GetProperty("activeDevices").GetInt32());
    }

    [Theory]
    [InlineData("not-a-token", "android")]
    [InlineData("ExponentPushToken[short]", "android")]
    [InlineData("ExponentPushToken[abcdefghijkl]", "windows")]
    [InlineData("", "ios")]
    public async Task Invalid_device_registrations_are_rejected(string token, string platform)
    {
        var (client, _) = await factory.NewUserAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/devices", new { token, platform })).StatusCode);
    }

    [Fact]
    public async Task Device_endpoints_require_a_session()
    {
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/devices", new { token = NewToken(), platform = "ios" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/v1/notifications/test", null)).StatusCode);
    }
}
