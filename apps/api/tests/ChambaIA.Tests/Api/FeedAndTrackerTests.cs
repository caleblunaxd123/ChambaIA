using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ChambaIA.Tests.Api;

/// <summary>The demo candidate end to end: seed → matching → feed → reactions → tracker → preference changes.</summary>
[Collection(ApiCollection.Name)]
public class FeedAndTrackerTests(ApiFactory factory)
{
    private sealed record Job(Guid Id, string Title, string? District);

    private sealed record Match(string Category, string Status, string[] TopReasons, string[] TopWarnings);

    private sealed record FeedItem(Job Job, Match? Match);

    private sealed record Page(FeedItem[] Items, int Page_, int PageSize, int Total, bool HasMore);

    private static async Task<(FeedItem[] Items, int Total, bool HasMore)> FeedAsync(HttpClient client, string url)
    {
        var json = await client.GetFromJsonAsync<JsonElement>(url);
        var items = json.GetProperty("items").Deserialize<FeedItem[]>(ApiFactory.Json)!;
        return (items, json.GetProperty("total").GetInt32(), json.GetProperty("hasMore").GetBoolean());
    }

    [Fact]
    public async Task Demo_user_gets_a_ranked_feed_with_a_realistic_spread_of_categories()
    {
        var client = await factory.DemoClientAsync();

        var (items, total, _) = await FeedAsync(client, "/api/v1/matches?pageSize=50");

        Assert.True(total >= 15, $"expected a rich feed, got {total}");
        Assert.DoesNotContain(items, i => i.Match!.Category == "poor");
        Assert.Contains(items, i => i.Match!.Category == "excellent");
        Assert.Contains(items, i => i.Match!.Category == "veryCompatible");
        Assert.Contains(items, i => i.Match!.Category == "compatible");
        Assert.Contains(items, i => i.Match!.Category == "review");

        var order = new[] { "excellent", "veryCompatible", "compatible", "review" };
        var ranks = items.Select(i => Array.IndexOf(order, i.Match!.Category)).ToArray();
        Assert.Equal(ranks.OrderBy(r => r), ranks);
    }

    [Fact]
    public async Task The_product_example_job_explains_its_risk_in_plain_spanish()
    {
        var client = await factory.DemoClientAsync();
        var (items, _, _) = await FeedAsync(client, "/api/v1/matches?pageSize=50&q=clinica santa aurora");
        var admin = items.Single(i => i.Job.Title == "Asistente Administrativa");

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{admin.Job.Id}");
        var match = detail.GetProperty("match");

        Assert.Equal("veryCompatible", match.GetProperty("category").GetString());
        Assert.Equal("Muy compatible", match.GetProperty("categoryLabel").GetString());
        var warnings = match.GetProperty("warnings").EnumerateArray().Select(w => w.GetProperty("title").GetString()).ToArray();
        Assert.Contains(warnings, w => w!.Contains("Excel", StringComparison.OrdinalIgnoreCase) && w.Contains("intermedio"));
        var reasons = match.GetProperty("reasons").EnumerateArray().Select(r => r.GetProperty("title").GetString()).ToArray();
        Assert.Contains(reasons, r => r!.StartsWith("Piden 2 años de experiencia"));
        Assert.False(string.IsNullOrWhiteSpace(match.GetProperty("recommendation").GetString()));
    }

    [Fact]
    public async Task Overview_summarises_new_and_total_matches()
    {
        var client = await factory.DemoClientAsync();

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/v1/matches/overview");

        Assert.True(overview.GetProperty("strong").GetInt32() >= 5);
        Assert.True(overview.GetProperty("notRecommended").GetInt32() >= 3);
        Assert.True(overview.GetProperty("newTotal").GetInt32() >= overview.GetProperty("newStrong").GetInt32());
    }

    [Fact]
    public async Task Filters_and_paging_work_and_invalid_values_are_a_400()
    {
        var client = await factory.DemoClientAsync();

        var (page1, total, hasMore) = await FeedAsync(client, "/api/v1/jobs?pageSize=5&page=1");
        var (page2, _, _) = await FeedAsync(client, "/api/v1/jobs?pageSize=5&page=2");
        Assert.Equal(5, page1.Length);
        Assert.True(hasMore);
        Assert.True(total >= 25);
        Assert.Empty(page1.Select(i => i.Job.Id).Intersect(page2.Select(i => i.Job.Id)));

        var (olivos, _, _) = await FeedAsync(client, "/api/v1/jobs?district=los%20olivos&pageSize=50");
        Assert.NotEmpty(olivos);
        Assert.All(olivos, i => Assert.Equal("Los Olivos", i.Job.District));

        var (remote, _, _) = await FeedAsync(client, "/api/v1/jobs?modality=remote");
        Assert.All(remote, i => Assert.NotNull(i.Job.Title));
        Assert.NotEmpty(remote);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/jobs?modality=nonsense")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/matches?tab=nonsense")).StatusCode);
    }

    [Fact]
    public async Task Interested_saves_the_job_and_creates_a_tracker_card_and_dismiss_hides_it()
    {
        var client = await factory.DemoClientAsync();
        var (items, _, _) = await FeedAsync(client, "/api/v1/matches?pageSize=50");
        var keep = items[0].Job;
        var drop = items[1].Job;

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/matches/{keep.Id}/interested", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/matches/{drop.Id}/dismiss", null)).StatusCode);

        var (saved, _, _) = await FeedAsync(client, "/api/v1/matches?tab=saved");
        Assert.Contains(saved, i => i.Job.Id == keep.Id);

        var (forYou, _, _) = await FeedAsync(client, "/api/v1/matches?pageSize=50");
        Assert.DoesNotContain(forYou, i => i.Job.Id == drop.Id);

        var applications = await client.GetFromJsonAsync<JsonElement>("/api/v1/applications?status=interested");
        Assert.Contains(applications.EnumerateArray(), a => a.GetProperty("jobId").GetGuid() == keep.Id);

        // Leave the demo data as we found it for other tests.
        await client.PostAsync($"/api/v1/matches/{keep.Id}/dismiss", null);
    }

    [Fact]
    public async Task Tracker_walks_through_the_pipeline_and_stamps_the_application_date()
    {
        var client = await factory.DemoClientAsync();
        var (items, _, _) = await FeedAsync(client, "/api/v1/matches?pageSize=50&tab=forYou");
        var job = items.Last().Job;

        var created = await client.PostAsJsonAsync("/api/v1/applications", new { jobId = job.Id, status = "interested", notes = "Llamar el lunes" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var card = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = card.GetProperty("id").GetGuid();

        var patch = await client.PatchAsJsonAsync($"/api/v1/applications/{id}", new { status = "applied" });
        var applied = await patch.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("applied", applied.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, applied.GetProperty("appliedAt").ValueKind);
        Assert.Equal("Llamar el lunes", applied.GetProperty("notes").GetString());

        var interview = await client.PatchAsJsonAsync($"/api/v1/applications/{id}",
            new { status = "interview", interviewDate = DateTimeOffset.UtcNow.AddDays(3) });
        Assert.Equal("interview", (await interview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        var match = await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{job.Id}");
        Assert.Equal("interview", match.GetProperty("match").GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/applications/{id}")).StatusCode);
    }

    [Fact]
    public async Task Users_cannot_read_or_change_each_others_applications()
    {
        var owner = await factory.DemoClientAsync();
        var (items, _, _) = await FeedAsync(owner, "/api/v1/matches?pageSize=50");
        var created = await owner.PostAsJsonAsync("/api/v1/applications", new { jobId = items[2].Job.Id });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var (stranger, _) = await factory.NewUserAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PatchAsJsonAsync($"/api/v1/applications/{id}", new { status = "offer" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/v1/applications/{id}")).StatusCode);
        var theirList = await stranger.GetFromJsonAsync<JsonElement>("/api/v1/applications");
        Assert.Equal(0, theirList.GetArrayLength());

        var otherMatch = await stranger.GetAsync($"/api/v1/matches/{items[2].Job.Id}");
        Assert.Equal(HttpStatusCode.NotFound, otherMatch.StatusCode);

        await owner.DeleteAsync($"/api/v1/applications/{id}");
    }

    [Fact]
    public async Task Changing_preferences_changes_the_feed_immediately()
    {
        var (client, _) = await factory.NewUserAsync();

        // A new user has no matches until profile and preferences exist.
        var (empty, total0, _) = await FeedAsync(client, "/api/v1/matches?pageSize=50");
        Assert.Empty(empty);
        Assert.Equal(0, total0);

        var profile = await client.PutAsJsonAsync("/api/v1/profile", new
        {
            fullName = "Usuaria Test",
            headline = "Asistente administrativa",
            experienceMonths = 33,
            educationLevel = "technical",
            educationStatus = "completed",
            skills = new[]
            {
                new { key = "", name = "Atención al usuario", level = "advanced" },
                new { key = "", name = "Gestión documentaria", level = "intermediate" },
                new { key = "", name = "Excel", level = "basic" }
            }
        });
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);

        object Prefs(string[] excluded) => new
        {
            minSalary = 1800,
            preferredRoles = new[] { "Asistente Administrativo" },
            excludedDistricts = excluded,
            homeDistrict = "Los Olivos",
            notificationFrequency = "daily"
        };

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/v1/preferences", Prefs([]))).StatusCode);
        var (before, _, _) = await FeedAsync(client, "/api/v1/matches?pageSize=50");
        Assert.NotEmpty(before);
        Assert.Contains(before, i => i.Job.District == "Ate");

        // "No me muestres trabajos en Ate."
        var saved = await client.PutAsJsonAsync("/api/v1/preferences", Prefs(["ate"]));
        var prefs = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ate", prefs.GetProperty("excludedDistricts")[0].GetString()); // canonicalised

        var (after, _, _) = await FeedAsync(client, "/api/v1/matches?pageSize=50");
        Assert.DoesNotContain(after, i => i.Job.District == "Ate");
        Assert.True(after.Length < before.Length);
    }

    [Fact]
    public async Task Profile_skills_are_normalised_to_canonical_keys()
    {
        var (client, _) = await factory.NewUserAsync();

        var response = await client.PutAsJsonAsync("/api/v1/profile", new
        {
            fullName = "Usuaria Test",
            experienceMonths = 12,
            skills = new[]
            {
                new { key = "", name = "servicio al cliente", level = "intermediate" },
                new { key = "", name = "Atención al usuario", level = "advanced" },
                new { key = "", name = "Soldadura TIG", level = "basic" }
            }
        });
        var skills = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("skills");

        Assert.Equal(2, skills.GetArrayLength()); // both aliases collapsed into one canonical skill + the custom one
        var canonical = skills.EnumerateArray().Single(s => s.GetProperty("key").GetString() == "atencion-al-cliente");
        Assert.Equal("advanced", canonical.GetProperty("level").GetString());
        Assert.Contains(skills.EnumerateArray(), s => s.GetProperty("key").GetString() == "soldadura-tig");
    }

    [Fact]
    public async Task The_agent_applies_the_literal_examples_from_the_product_brief()
    {
        var (client, _) = await factory.NewUserAsync();
        await client.PutAsJsonAsync("/api/v1/profile", new
        {
            fullName = "Usuaria Test", experienceMonths = 33, headline = "Asistente administrativa",
            skills = new[] { new { key = "", name = "Atención al usuario", level = "advanced" } }
        });
        await client.PutAsJsonAsync("/api/v1/preferences", new { minSalary = 1500, preferredRoles = new[] { "Asistente Administrativo" } });

        async Task<JsonElement> Say(string text)
        {
            var response = await client.PostAsJsonAsync("/api/v1/agent/messages", new { text });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        var (before, _, _) = await FeedAsync(client, "/api/v1/matches?pageSize=50");
        Assert.Contains(before, i => i.Job.District == "Ate");

        var ate = await Say("No me muestres trabajos en Ate.");
        Assert.True(ate.GetProperty("understood").GetBoolean());
        Assert.Contains("Ate", ate.GetProperty("reply").GetString());
        var (afterAte, _, _) = await FeedAsync(client, "/api/v1/matches?pageSize=50");
        Assert.DoesNotContain(afterAte, i => i.Job.District == "Ate");

        await Say("mínimo 2200 soles");
        var prefs = await client.GetFromJsonAsync<JsonElement>("/api/v1/preferences");
        Assert.Equal(2200, prefs.GetProperty("minSalary").GetDecimal());
        Assert.Contains("Ate", prefs.GetProperty("excludedDistricts").EnumerateArray().Select(e => e.GetString()));

        await Say("no quiero call center");
        var (afterCallCenter, _, _) = await FeedAsync(client, "/api/v1/matches?pageSize=50");
        Assert.DoesNotContain(afterCallCenter, i => i.Job.Title.Contains("Call Center", StringComparison.OrdinalIgnoreCase));

        var repeated = await Say("mínimo 2200 soles");
        Assert.Empty(repeated.GetProperty("changes").EnumerateArray());

        var chat = await Say("hola, ¿cómo estás?");
        Assert.False(chat.GetProperty("understood").GetBoolean());
    }

    [Fact]
    public async Task Catalog_exposes_districts_and_skills_for_pickers()
    {
        var (client, _) = await factory.NewUserAsync();

        var districts = await client.GetFromJsonAsync<string[]>("/api/v1/catalog/districts");
        var skills = await client.GetFromJsonAsync<JsonElement>("/api/v1/catalog/skills");

        Assert.Contains("Los Olivos", districts!);
        Assert.True(skills.GetArrayLength() > 20);
    }

    [Fact]
    public async Task Invalid_preferences_are_rejected()
    {
        var (client, _) = await factory.NewUserAsync();

        var response = await client.PutAsJsonAsync("/api/v1/preferences", new { minSalary = 3000, maxSalary = 1000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
