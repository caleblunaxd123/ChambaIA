using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ChambaIA.Tests.Api;

[Collection(ApiCollection.Name)]
public class ExplanationAndFeedTests(ApiFactory factory)
{
    private static async Task<JsonElement> JobByTitle(HttpClient client, string title, string company)
    {
        var feed = await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs?q={Uri.EscapeDataString(company)}&pageSize=50");
        return feed.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("job")).First(j => j.GetProperty("title").GetString() == title);
    }

    [Fact]
    public async Task Job_detail_explains_the_match_by_dimension_and_suggests_how_to_improve_it()
    {
        var client = await factory.DemoClientAsync();
        var job = await JobByTitle(client, "Asistente Administrativa", "Clínica Santa Aurora");

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{job.GetProperty("id").GetGuid()}");
        var match = detail.GetProperty("match");

        var dims = match.GetProperty("dimensions").EnumerateArray().ToDictionary(d => d.GetProperty("key").GetString()!);
        Assert.Equal(["role", "skills", "experience", "conditions"], dims.Keys.Order(StringComparer.Ordinal).OrderBy(k => Array.IndexOf(["role", "skills", "experience", "conditions"], k)));
        Assert.Equal("strong", dims["experience"].GetProperty("level").GetString());
        Assert.Contains("Piden 2 años", dims["experience"].GetProperty("note").GetString());
        Assert.DoesNotContain("meaning", dims.Keys); // no embeddings in this configuration

        var improvement = match.GetProperty("improvements").EnumerateArray().First();
        Assert.Contains("Excel", improvement.GetProperty("title").GetString());
        Assert.Equal("Excelente opción", improvement.GetProperty("resultLabel").GetString());
        Assert.StartsWith("Solo si ya lo sabes hacer.", improvement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task The_same_explanation_is_served_by_the_matches_endpoint()
    {
        var client = await factory.DemoClientAsync();
        var job = await JobByTitle(client, "Asistente Administrativa", "Clínica Santa Aurora");

        var response = await client.GetFromJsonAsync<JsonElement>($"/api/v1/matches/{job.GetProperty("id").GetGuid()}");

        Assert.Equal(4, response.GetProperty("match").GetProperty("dimensions").GetArrayLength());
    }

    [Fact]
    public async Task Hard_filtered_offers_show_a_weak_conditions_axis_and_no_improvements()
    {
        var client = await factory.DemoClientAsync();
        var job = await JobByTitle(client, "Cajera", "Supermercados Mi Barrio");

        var match = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{job.GetProperty("id").GetGuid()}")).GetProperty("match");

        var conditions = match.GetProperty("dimensions").EnumerateArray().Single(d => d.GetProperty("key").GetString() == "conditions");
        Assert.Equal("weak", conditions.GetProperty("level").GetString());
        Assert.Equal(0, match.GetProperty("improvements").GetArrayLength());
    }

    [Fact]
    public async Task Offers_without_a_match_still_open_and_have_no_explanation_to_show()
    {
        var (client, _) = await factory.NewUserAsync(); // no profile data yet, so no matches
        var feed = await client.GetFromJsonAsync<JsonElement>("/api/v1/jobs?pageSize=1");
        var id = feed.GetProperty("items")[0].GetProperty("job").GetProperty("id").GetGuid();

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{id}");

        Assert.Equal(JsonValueKind.Null, detail.GetProperty("match").ValueKind);
    }

    /// <summary>Offers that state a salary come first, highest first; offers that do not say go last (other tests add some to the shared database).</summary>
    private static void AssertSalaryOrder(JsonElement page)
    {
        var tops = page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("job").GetProperty("salaryMax") is { ValueKind: JsonValueKind.Number } n ? n.GetDecimal() : (decimal?)null).ToList();
        var stated = tops.TakeWhile(t => t is not null).Select(t => t!.Value).ToList();

        Assert.True(stated.Count > 5);
        Assert.Equal(stated.OrderByDescending(s => s), stated);
        Assert.All(tops.Skip(stated.Count), t => Assert.Null(t));
    }

    [Fact]
    public async Task Feed_can_be_sorted_by_recency_and_by_salary_and_rejects_unknown_values()
    {
        var client = await factory.DemoClientAsync();

        var recent = await client.GetFromJsonAsync<JsonElement>("/api/v1/matches?sort=recent&pageSize=50");
        var dates = recent.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("job").GetProperty("postedAt").GetDateTimeOffset()).ToList();
        Assert.True(dates.Count > 5);
        Assert.Equal(dates.OrderByDescending(d => d), dates);

        var salary = await client.GetFromJsonAsync<JsonElement>("/api/v1/matches?sort=salary&pageSize=50");
        AssertSalaryOrder(salary);

        var jobsBySalary = await client.GetFromJsonAsync<JsonElement>("/api/v1/jobs?sort=salary&pageSize=50");
        AssertSalaryOrder(jobsBySalary);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/matches?sort=sideways")).StatusCode);
    }

    [Fact]
    public async Task Relevance_is_the_default_order_of_the_for_you_tab()
    {
        var client = await factory.DemoClientAsync();

        var plain = await client.GetFromJsonAsync<JsonElement>("/api/v1/matches?pageSize=50");
        var explicitRelevance = await client.GetFromJsonAsync<JsonElement>("/api/v1/matches?sort=relevance&pageSize=50");

        static IEnumerable<Guid> Ids(JsonElement e) => e.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("job").GetProperty("id").GetGuid());
        Assert.Equal(Ids(plain), Ids(explicitRelevance));
    }

    [Fact]
    public async Task Similar_offers_without_embeddings_fall_back_to_the_same_company_or_sector()
    {
        var client = await factory.DemoClientAsync();
        var job = await JobByTitle(client, "Asistente Administrativa", "Clínica Santa Aurora");
        var id = job.GetProperty("id").GetGuid();

        var similar = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{id}/similar?limit=5")).EnumerateArray().ToList();

        Assert.NotEmpty(similar);
        Assert.True(similar.Count <= 5);
        Assert.DoesNotContain(similar, s => s.GetProperty("job").GetProperty("id").GetGuid() == id);
        Assert.All(similar, s =>
        {
            var j = s.GetProperty("job");
            Assert.True(j.GetProperty("company").GetString() == "Clínica Santa Aurora" || j.GetProperty("industry").GetString() == "Salud");
        });
    }

    [Fact]
    public async Task Similar_offers_hide_what_the_user_already_dismissed_and_validate_ids()
    {
        var client = await factory.DemoClientAsync();
        var job = await JobByTitle(client, "Asistente Administrativa", "Clínica Santa Aurora");
        var id = job.GetProperty("id").GetGuid();
        var first = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{id}/similar")).EnumerateArray().First().GetProperty("job").GetProperty("id").GetGuid();

        await client.PostAsync($"/api/v1/matches/{first}/dismiss", null);
        var after = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{id}/similar")).EnumerateArray()
            .Select(s => s.GetProperty("job").GetProperty("id").GetGuid()).ToList();

        Assert.DoesNotContain(first, after);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/jobs/{Guid.NewGuid()}/similar")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/v1/jobs/{id}/similar")).StatusCode);
    }
}

/// <summary>With embeddings the explanation gains a "meaning" axis and similar offers come from pgvector's nearest neighbours.</summary>
[Collection(EmbeddingApiCollection.Name)]
public class SemanticExplanationTests(EmbeddingApiFactory factory)
{
    [Fact]
    public async Task Detail_includes_the_meaning_axis_when_the_match_has_a_semantic_score()
    {
        var client = await factory.DemoClientAsync();
        await client.PostAsync("/api/v1/matches/refresh", null);
        var feed = await client.GetFromJsonAsync<JsonElement>("/api/v1/jobs?q=clinica%20santa%20aurora&pageSize=5");
        var id = feed.GetProperty("items")[0].GetProperty("job").GetProperty("id").GetGuid();

        var dims = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{id}")).GetProperty("match").GetProperty("dimensions")
            .EnumerateArray().Select(d => d.GetProperty("key").GetString()).ToList();

        Assert.Contains("meaning", dims);
    }

    [Fact]
    public async Task Similar_offers_are_the_nearest_vectors_and_closer_than_unrelated_trades()
    {
        var client = await factory.DemoClientAsync();
        var feed = await client.GetFromJsonAsync<JsonElement>("/api/v1/jobs?q=asistente%20administrativa&pageSize=10");
        var admin = feed.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("job"))
            .First(j => j.GetProperty("title").GetString() == "Asistente Administrativa");

        var titles = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{admin.GetProperty("id").GetGuid()}/similar?limit=4"))
            .EnumerateArray().Select(s => s.GetProperty("job").GetProperty("title").GetString()!).ToList();

        Assert.Equal(4, titles.Count);
        Assert.Contains(titles, t => t.Contains("Administrativ", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Cajera", titles);
        Assert.DoesNotContain("Contador Público Colegiado", titles);
    }
}
