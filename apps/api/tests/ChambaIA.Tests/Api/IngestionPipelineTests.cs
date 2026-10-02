using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Ingestion;
using ChambaIA.Infrastructure.Ingestion;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChambaIA.Tests.Api;

/// <summary>Phase 3 end to end: sources → normaliser → 3-layer dedup → storage → retirement → matching.</summary>
[Collection(ApiCollection.Name)]
public class IngestionPipelineTests(ApiFactory factory)
{
    private sealed class FakeSource(string key, Func<IReadOnlyList<RawJob>> jobs) : IJobSource
    {
        public string Key => key;
        public string Name => $"Fuente {key}";
        public JobSourceKind Kind => JobSourceKind.Feed;
        public string? BaseUrl => null;
        public Task<IReadOnlyList<RawJob>> FetchJobsAsync(DateTimeOffset? since, CancellationToken ct) => Task.FromResult(jobs());
    }

    private sealed class BrokenSource(string key) : IJobSource
    {
        public string Key => key;
        public string Name => "Rota";
        public JobSourceKind Kind => JobSourceKind.Feed;
        public string? BaseUrl => null;
        public Task<IReadOnlyList<RawJob>> FetchJobsAsync(DateTimeOffset? since, CancellationToken ct) => throw new HttpRequestException("503 Service Unavailable");
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private static RawJob Job(string id, string company, string title = "Asistente Administrativa", string district = "San Miguel", string? description = null, Func<RawJob, RawJob>? tweak = null)
    {
        var raw = new RawJob
        {
            ExternalId = id,
            Title = title,
            Company = company,
            Description = description ?? "Atención al cliente y gestión documentaria. Excel intermedio. Lunes a viernes. S/ 1,900 - 2,200.",
            Url = $"https://empleos.example.com/{id}",
            Location = $"{district}, Lima"
        };
        return tweak is null ? raw : tweak(raw);
    }

    private async Task<T> WithIngestion<T>(Func<IngestionService, AppDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IngestionService>(), scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<IngestionReport> Run(params IJobSource[] sources) => WithIngestion((s, _) => s.RunAsync(sources, recompute: true));

    [Fact]
    public async Task Ingestion_is_idempotent_and_updates_offers_in_place()
    {
        var company = Unique("Idempotente");
        var description = "Atención al cliente. Lunes a viernes.";
        var source = new FakeSource(Unique("feed"), () => [Job("a-1", company, description: description), Job("a-2", company, title: "Cajero Principal")]);

        var first = await Run(source);
        Assert.Equal(2, first.Sources[0].Created);

        var second = await Run(source);
        Assert.Equal((0, 0, 2), (second.Sources[0].Created, second.Sources[0].Updated, second.Sources[0].Unchanged));

        description = "Atención al cliente. Lunes a sábado.";
        var third = await Run(source);
        Assert.Equal(1, third.Sources[0].Updated);

        var stored = await WithIngestion((_, db) => db.JobOffers.SingleAsync(j => j.Company == company && j.ExternalId == "a-1"));
        Assert.False(stored.WeekdaysOnly);
    }

    [Fact]
    public async Task The_same_offer_on_two_portals_is_stored_once_as_active()
    {
        var company = Unique("Clinica");
        var portalA = new FakeSource(Unique("portal-a"), () => [Job("100", company)]);
        var portalB = new FakeSource(Unique("portal-b"), () =>
        [
            Job("x-9", company),                                                           // layer 2: identical content
            Job("x-10", company, title: "Auxiliar Administrativo", description: "Se busca auxiliar con experiencia en archivo.") // layer 3: reworded
        ]);

        await Run(portalA);
        var report = await Run(portalB);
        Assert.Equal(2, report.Sources[0].Duplicates);
        Assert.Equal(0, report.Sources[0].Created);

        var offers = await WithIngestion((_, db) => db.JobOffers.Where(j => j.Company == company).ToListAsync());
        var original = offers.Single(o => o.DuplicateOfId is null);
        Assert.True(original.IsActive);
        Assert.All(offers.Where(o => o.DuplicateOfId is not null), d =>
        {
            Assert.False(d.IsActive);
            Assert.Equal(original.Id, d.DuplicateOfId);
        });

        // Re-running the second portal keeps them as duplicates (never resurrected as separate offers).
        var again = await Run(portalB);
        Assert.Equal(0, again.Sources[0].Created);
        Assert.Equal(1, await WithIngestion((_, db) => db.JobOffers.CountAsync(j => j.Company == company && j.IsActive)));
    }

    [Fact]
    public async Task Feeds_with_local_time_zones_are_stored()
    {
        var company = Unique("Horario");
        var lima = TimeSpan.FromHours(-5);
        var report = await Run(new FakeSource(Unique("lima"), () =>
            [Job("tz-1", company, tweak: r => r with { PostedAt = DateTimeOffset.UtcNow.ToOffset(lima).AddHours(-2), ExpiresAt = DateTimeOffset.UtcNow.ToOffset(lima).AddDays(20) })]));

        Assert.Null(report.Sources[0].Error);
        Assert.Equal(1, report.Sources[0].Created);
    }

    [Fact]
    public async Task A_broken_source_is_reported_and_does_not_stop_the_others()
    {
        var brokenKey = Unique("rota");
        var company = Unique("Sana");
        var report = await Run(new BrokenSource(brokenKey), new FakeSource(Unique("sana"), () => [Job("ok-1", company)]));

        Assert.Contains("503", report.Sources[0].Error);
        Assert.Null(report.Sources[1].Error);
        Assert.Equal(1, report.Sources[1].Created);

        var stored = await WithIngestion((_, db) => db.JobSources.SingleAsync(s => s.Key == brokenKey));
        Assert.Contains("503", stored.LastError);
        Assert.Null(stored.LastFetchedAt);
    }

    [Fact]
    public async Task Expired_and_abandoned_offers_are_retired()
    {
        var company = Unique("Retiro");
        var listing = new List<RawJob>
        {
            Job("keep", company, title: "Recepcionista"),
            Job("gone", company, title: "Almacenero"),
            Job("old", company, title: "Mensajero", tweak: r => r with { PostedAt = DateTimeOffset.UtcNow.AddDays(-40), ExpiresAt = DateTimeOffset.UtcNow.AddDays(-5) })
        };
        var source = new FakeSource(Unique("retiro"), () => listing.ToList());
        await Run(source);

        // The source stops listing "gone" and we pretend it was last seen 10 days ago.
        listing.RemoveAll(j => j.ExternalId == "gone");
        await WithIngestion((_, db) => db.JobOffers.Where(j => j.Company == company && j.ExternalId == "gone")
            .ExecuteUpdateAsync(u => u.SetProperty(j => j.LastSeenAt, DateTimeOffset.UtcNow.AddDays(-10))));
        var report = await Run(source);

        Assert.True(report.Deactivated >= 1);
        var active = await WithIngestion((_, db) => db.JobOffers.Where(j => j.Company == company && j.IsActive).Select(j => j.ExternalId).ToListAsync());
        Assert.Equal(["keep"], active);
    }

    [Fact]
    public async Task New_offers_reach_the_feed_of_matching_users_right_away()
    {
        var (client, _) = await factory.NewUserAsync();
        await client.PutAsJsonAsync("/api/v1/profile", new
        {
            fullName = "Usuaria Ingesta", experienceMonths = 36, educationLevel = "technical", educationStatus = "completed",
            skills = new[] { new { key = "", name = "Facturación", level = "advanced" }, new { key = "", name = "Excel", level = "intermediate" } }
        });
        await client.PutAsJsonAsync("/api/v1/preferences", new { preferredRoles = new[] { "Asistente de Facturación" }, notificationFrequency = "daily" });

        var company = Unique("Facturas");
        await Run(new FakeSource(Unique("fact"), () =>
        [
            Job("f-1", company, title: "Asistente de Facturación", district: "Los Olivos",
                description: "Emisión de facturas electrónicas. Excel intermedio. 1 año de experiencia. S/ 2,000 - 2,400.")
        ]));

        var feed = await client.GetFromJsonAsync<JsonElement>("/api/v1/matches?pageSize=50&q=" + Uri.EscapeDataString(company));
        var item = Assert.Single(feed.GetProperty("items").EnumerateArray());
        Assert.Equal("Asistente de Facturación", item.GetProperty("job").GetProperty("title").GetString());
        Assert.Equal(2000, item.GetProperty("job").GetProperty("salaryMin").GetDecimal());
        Assert.NotEqual("poor", item.GetProperty("match").GetProperty("category").GetString());
    }

    [Fact]
    public async Task Sources_endpoint_shows_where_offers_come_from_without_internal_errors()
    {
        var client = await factory.DemoClientAsync();

        var sources = await client.GetFromJsonAsync<JsonElement>("/api/v1/sources");
        var demo = sources.EnumerateArray().Single(s => s.GetProperty("key").GetString() == DemoJobSource.SourceKey);
        Assert.True(demo.GetProperty("activeOffers").GetInt32() >= 20);
        Assert.True(demo.GetProperty("healthy").GetBoolean());
        Assert.False(demo.TryGetProperty("lastError", out _));

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/sources")).StatusCode);
        // Manual ingestion is a Development convenience only.
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/sources/ingest", null)).StatusCode);
    }

    [Fact]
    public async Task Interview_date_can_be_removed_explicitly()
    {
        var client = await factory.DemoClientAsync();
        var feed = await client.GetFromJsonAsync<JsonElement>("/api/v1/matches?pageSize=50");
        var jobId = feed.GetProperty("items")[8].GetProperty("job").GetProperty("id").GetGuid();

        var created = await client.PostAsJsonAsync("/api/v1/applications", new { jobId, status = "applied" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await client.PatchAsJsonAsync($"/api/v1/applications/{id}", new { status = "interview", interviewDate = DateTimeOffset.UtcNow.AddDays(2) });

        var untouched = await (await client.PatchAsJsonAsync($"/api/v1/applications/{id}", new { notes = "Llevar DNI" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(JsonValueKind.Null, untouched.GetProperty("interviewDate").ValueKind);

        var cleared = await (await client.PatchAsJsonAsync($"/api/v1/applications/{id}", new { clearInterviewDate = true })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("interviewDate").ValueKind);
        Assert.Equal("Llevar DNI", cleared.GetProperty("notes").GetString());

        await client.DeleteAsync($"/api/v1/applications/{id}");
    }
}
