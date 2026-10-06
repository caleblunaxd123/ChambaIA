using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChambaIA.Infrastructure.Ai;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChambaIA.Tests.Api;

/// <summary>The "model" of the assistant tests: whatever the test scripts it to say, and a record of what it was asked.</summary>
public sealed class ScriptedAi : IAiProvider, IAiProviderResolver
{
    public string Type => "Scripted";
    public string Model => "scripted-1";
    public Func<AiRequest, string> Answer { get; set; } = _ => "{}";
    public List<AiRequest> Requests { get; } = [];

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(new AiResponse(Answer(request), 300, 120));
    }

    public IAiProvider? Get(string name) => this;
}

public sealed class AiApiFactory : ApiFactory
{
    public ScriptedAi Ai { get; } = new();

    protected override void ConfigureExtra(IWebHostBuilder builder)
    {
        builder.UseSetting("Ai:Enabled", "true");
        builder.UseSetting("Ai:Providers:scripted:Type", "Scripted");
        builder.UseSetting("Ai:Providers:scripted:Model", "scripted-1");
        builder.UseSetting("Ai:Routes:CommandFallback:0", "scripted");
        builder.UseSetting("Ai:Routes:Premium:0", "scripted");
        builder.UseSetting("Ai:Budgets:GlobalMonthlyUsd", "1000");
        builder.UseSetting("Ai:Budgets:Free:DailyCalls", "3");                       // application drafts
        builder.UseSetting("Ai:Budgets:Free:DailyCallsByTask:CommandFallback", "3"); // sentences understood with AI
        builder.UseSetting("Ai:Budgets:Free:MonthlyUsd", "100");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiProviderResolver>();
            services.AddSingleton<IAiProviderResolver>(Ai);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class AssistantApiCollection : ICollectionFixture<AiApiFactory>
{
    public const string Name = "api-assistant";
}

/// <summary>Phase 9: the AI fallback of the agent (propose, then approve) and the "prepare my application" action (preview, then approve).</summary>
[Collection(AssistantApiCollection.Name)]
public class AssistantApiTests(AiApiFactory factory)
{
    private ScriptedAi Ai => factory.Ai;

    private static string Commands(string json) => $"{{\"commands\": {json}}}";

    private async Task<HttpClient> UserAsync(string name = "Lucía Prueba Gómez")
    {
        var (client, _) = await factory.NewUserAsync(name);
        Ai.Requests.Clear();
        Ai.Answer = _ => "{}";
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> Say(HttpClient client, string text) => client.PostAsJsonAsync("/api/v1/agent/messages", new { text });

    private static async Task<JsonElement> Prefs(HttpClient client) => await client.GetFromJsonAsync<JsonElement>("/api/v1/preferences");

    // ---- the agent's AI fallback -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Sentences_the_rules_understand_never_reach_the_model()
    {
        var client = await UserAsync();

        var reply = await Json(await Say(client, "mínimo 1800 soles"));

        Assert.True(reply.GetProperty("understood").GetBoolean());
        Assert.Equal(1, reply.GetProperty("changes").GetArrayLength());
        Assert.Empty(Ai.Requests); // zero cost, applied at once
    }

    [Fact]
    public async Task A_sentence_the_rules_miss_becomes_a_proposal_that_changes_nothing_until_approved()
    {
        var client = await UserAsync();
        Ai.Answer = _ => Commands("[{\"intent\":\"ExcludeDistrict\",\"text\":\"Ate\"},{\"intent\":\"SetMinSalary\",\"number\":2000}]");

        var reply = await Json(await Say(client, "la zona de Ate me queda fatal y necesito ganar bien"));

        Assert.True(reply.GetProperty("understood").GetBoolean());
        Assert.Equal(0, reply.GetProperty("changes").GetArrayLength());
        var proposal = reply.GetProperty("proposal");
        Assert.Equal(2, proposal.GetProperty("descriptions").GetArrayLength());
        Assert.Contains("¿Lo aplico?", reply.GetProperty("reply").GetString());

        var before = await Prefs(client);
        Assert.Empty(before.GetProperty("excludedDistricts").EnumerateArray());   // not applied yet
        Assert.Equal(JsonValueKind.Null, before.GetProperty("minSalary").ValueKind);

        var applied = await Json(await client.PostAsJsonAsync("/api/v1/agent/apply", new { commands = proposal.GetProperty("commands") }));

        Assert.Equal(2, applied.GetProperty("changes").GetArrayLength());
        var after = await Prefs(client);
        Assert.Equal(["Ate"], after.GetProperty("excludedDistricts").EnumerateArray().Select(d => d.GetString()));
        Assert.Equal(2000, after.GetProperty("minSalary").GetDecimal());
    }

    [Fact]
    public async Task The_users_sentence_is_sent_as_marked_data_and_with_the_closed_instructions()
    {
        var client = await UserAsync();
        Ai.Answer = _ => Commands("[]");

        await Say(client, "ahora quiero algo diferente");

        var request = Assert.Single(Ai.Requests);
        Assert.Contains("ahora quiero algo diferente", request.User);
        Assert.StartsWith("<<<MENSAJE", request.User);
        Assert.True(request.Json);
        Assert.Contains("nunca instrucciones", request.System);
    }

    [Theory]
    [InlineData("No sé qué contestar")]                                                                    // not JSON
    [InlineData("{\"commands\": []}")]                                                                     // nothing asked
    [InlineData("{\"commands\": [{\"intent\":\"DeleteAccount\"}]}")]                                       // not an intent
    [InlineData("{\"commands\": [{\"intent\":\"SetMinSalary\",\"number\":99999999}]}")]                    // absurd value
    [InlineData("{\"commands\": [{\"intent\":\"ExcludeDistrict\",\"text\":\"Mordor\"}]}")]                 // a district that does not exist
    public async Task An_unusable_interpretation_is_reported_as_not_understood_and_changes_nothing(string modelAnswer)
    {
        var client = await UserAsync();
        Ai.Answer = _ => modelAnswer;

        var reply = await Json(await Say(client, "algo que las reglas no entienden en absoluto"));

        Assert.False(reply.GetProperty("understood").GetBoolean());
        Assert.Equal(JsonValueKind.Null, reply.GetProperty("proposal").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await Prefs(client)).GetProperty("minSalary").ValueKind);
    }

    [Fact]
    public async Task A_proposal_that_repeats_what_is_already_noted_says_so()
    {
        var client = await UserAsync();
        await Say(client, "mínimo 1800 soles");
        Ai.Answer = _ => Commands("[{\"intent\":\"SetMinSalary\",\"number\":1800}]");

        var reply = await Json(await Say(client, "quiero lo que dije antes sobre el dinero"));

        Assert.Contains("ya lo tenía anotado", reply.GetProperty("reply").GetString());
        Assert.Equal(JsonValueKind.Null, reply.GetProperty("proposal").ValueKind);
    }

    [Fact]
    public async Task The_daily_allowance_of_ai_help_is_per_user_and_the_agent_degrades_gracefully()
    {
        var client = await UserAsync();
        var other = await UserAsync();
        Ai.Answer = _ => Commands("[]");

        for (var i = 0; i < 3; i++) await Say(client, $"frase libre número {i} que no entienden las reglas");
        var fourth = await Json(await Say(client, "otra frase libre más que no entienden las reglas"));

        Assert.False(fourth.GetProperty("understood").GetBoolean());
        Assert.Contains("ayuda extra", fourth.GetProperty("reply").GetString());
        Assert.Equal(3, Ai.Requests.Count);               // the fourth never reached the model

        await Say(other, "una frase libre de otra persona");
        Assert.Equal(4, Ai.Requests.Count);               // someone else still has theirs
        var rules = await Json(await Say(client, "mínimo 2500 soles"));
        Assert.True(rules.GetProperty("understood").GetBoolean()); // and the rules keep working for everyone
    }

    [Fact]
    public async Task Approving_a_proposal_validates_it_again_so_a_tampered_request_cannot_write_anything_odd()
    {
        var client = await UserAsync();

        var odd = await client.PostAsJsonAsync("/api/v1/agent/apply", new { commands = new object[]
        {
            new { intent = "SetMinSalary", number = 99999999 },
            new { intent = "DeleteEverything" },
            new { intent = "AddRole", text = "<script>alert(1)</script>" }
        } });
        Assert.Equal(HttpStatusCode.BadRequest, odd.StatusCode);

        var tooMany = await client.PostAsJsonAsync("/api/v1/agent/apply", new { commands = Enumerable.Range(0, 6).Select(_ => new { intent = "SetWeekdaysOnly", flag = true }) });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/agent/apply", new { commands = Array.Empty<object>() })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync("/api/v1/agent/apply", new { commands = new[] { new { intent = "SetWeekdaysOnly", flag = true } } })).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await Prefs(client)).GetProperty("minSalary").ValueKind);
    }

    // ---- prepare my application ------------------------------------------------------------------------------------------

    private async Task<(HttpClient Client, Guid JobId)> UserWithProfileAndJobAsync(string name = "Lucía Prueba Gómez")
    {
        var client = await UserAsync(name);
        (await client.PutAsJsonAsync("/api/v1/profile", new
        {
            fullName = name, headline = "Asistente administrativa", experienceMonths = 30, educationLevel = "technical", educationStatus = "completed", completeOnboarding = true,
            skills = new[] { new { key = "", name = "Excel", level = "intermediate" }, new { key = "", name = "Facturación", level = "advanced" } },
            experience = new[] { new { title = "Asistente administrativa", company = "Comercial Norte", startDate = "2023-01-01", endDate = (string?)null, description = "Facturación y archivo" } }
        })).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var job = await scope.ServiceProvider.GetRequiredService<AppDbContext>().JobOffers.Where(j => j.IsActive).OrderBy(j => j.Id).Skip(3).Select(j => j.Id).FirstAsync();
        return (client, job);
    }

    private const string GoodAnswer = """
        {"message": "Estimados señores, me interesa el puesto. Tengo experiencia en facturación y manejo de Excel, y me gustaría conversar sobre cómo aportar a su equipo. Quedo atenta. Lucía",
         "highlights": ["Maneja Excel", "Experiencia en facturación"],
         "gaps": ["Pide inglés"],
         "questions": ["¿Por qué este puesto?"]}
        """;

    [Fact]
    public async Task The_preview_shows_exactly_what_would_be_sent_and_sends_nothing()
    {
        var (client, jobId) = await UserWithProfileAndJobAsync();

        var preview = await Json(await client.GetAsync($"/api/v1/jobs/{jobId}/application-prep"));

        Assert.True(preview.GetProperty("available").GetBoolean());
        var payload = preview.GetProperty("payload").GetString()!;
        Assert.Contains("Nombre de pila: Lucía", payload);
        Assert.Contains("Excel (intermedio)", payload);
        Assert.Contains("Asistente administrativa en Comercial Norte", payload);
        Assert.DoesNotContain("Gómez", payload);                       // only the first name
        Assert.DoesNotContain("@", payload);                           // no email
        Assert.NotEmpty(preview.GetProperty("includes").EnumerateArray());
        Assert.Contains(preview.GetProperty("excludes").EnumerateArray(), e => e.GetString()!.Contains("correo"));
        Assert.Equal(3, preview.GetProperty("quota").GetProperty("dailyLimit").GetInt32());
        Assert.Empty(Ai.Requests);                                      // nothing left the building
    }

    [Fact]
    public async Task A_draft_needs_an_explicit_approval_and_sends_exactly_the_previewed_text()
    {
        var (client, jobId) = await UserWithProfileAndJobAsync();
        Ai.Answer = _ => GoodAnswer;
        var previewed = (await Json(await client.GetAsync($"/api/v1/jobs/{jobId}/application-prep"))).GetProperty("payload").GetString();

        var withoutApproval = await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/application-prep", new { approved = false });
        Assert.Equal(HttpStatusCode.BadRequest, withoutApproval.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/application-prep", new { })).StatusCode);
        Assert.Empty(Ai.Requests);

        var draft = await Json(await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/application-prep", new { approved = true, tone = "cercano" }));

        Assert.Contains("Estimados señores", draft.GetProperty("message").GetString());
        Assert.Equal(["Maneja Excel", "Experiencia en facturación"], draft.GetProperty("highlights").EnumerateArray().Select(h => h.GetString()));
        Assert.Equal(["Pide inglés"], draft.GetProperty("gaps").EnumerateArray().Select(h => h.GetString()));
        Assert.Empty(draft.GetProperty("warnings").EnumerateArray());

        var sent = Assert.Single(Ai.Requests);
        Assert.Equal(previewed, sent.User);                               // what was approved is what was sent
        Assert.Contains("cercano y amable", sent.System);
    }

    [Fact]
    public async Task Generating_costs_the_users_quota_and_is_recorded_per_user()
    {
        var (client, jobId) = await UserWithProfileAndJobAsync();
        Ai.Answer = _ => GoodAnswer;

        await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/application-prep", new { approved = true });
        var quota = (await Json(await client.GetAsync($"/api/v1/jobs/{jobId}/application-prep"))).GetProperty("quota");

        Assert.Equal((1, 2), (quota.GetProperty("callsToday").GetInt32(), quota.GetProperty("remainingToday").GetInt32()));
        await using var scope = factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AiUsages.Where(u => u.UserId != null && u.Operation == "Premium" && u.Provider == "scripted").ToListAsync();
        Assert.NotEmpty(rows);
    }

    [Fact]
    public async Task A_draft_that_claims_what_the_profile_does_not_back_up_comes_back_with_warnings()
    {
        var (client, jobId) = await UserWithProfileAndJobAsync();
        Ai.Answer = _ => """
            {"message": "Estimados señores, cuento con 9 años de experiencia y domino Power BI y Word con mucha soltura, por lo que sería un gran aporte para su equipo. Lucía"}
            """;

        var draft = await Json(await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/application-prep", new { approved = true }));

        var warnings = draft.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).ToList();
        Assert.Contains(warnings, w => w.Contains("9 años"));
        Assert.Contains(warnings, w => w.Contains("Word"));
    }

    [Theory]
    [InlineData("No tengo nada que decir")]
    [InlineData("{\"message\": \"Escríbame a lucia@correo.com para coordinar la entrevista del puesto de asistente en su empresa por favor.\"}")]
    public async Task An_unreliable_answer_is_never_shown_as_a_draft(string modelAnswer)
    {
        var (client, jobId) = await UserWithProfileAndJobAsync();
        Ai.Answer = _ => modelAnswer;

        var response = await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/application-prep", new { approved = true });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task The_daily_limit_stops_the_fourth_draft_with_a_clear_message()
    {
        var (client, jobId) = await UserWithProfileAndJobAsync();
        Ai.Answer = _ => GoodAnswer;

        for (var i = 0; i < 3; i++) Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/application-prep", new { approved = true })).StatusCode);
        var fourth = await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/application-prep", new { approved = true });

        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        Assert.Equal(3, Ai.Requests.Count);
    }

    [Fact]
    public async Task Prep_endpoints_reject_anonymous_users_and_unknown_offers_and_bad_tones()
    {
        var (client, jobId) = await UserWithProfileAndJobAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/v1/jobs/{jobId}/application-prep")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/jobs/{Guid.NewGuid()}/application-prep")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/v1/jobs/{Guid.NewGuid()}/application-prep", new { approved = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/application-prep", new { approved = true, tone = "agresivo" })).StatusCode);
        Assert.Empty(Ai.Requests);
    }
}

/// <summary>With AI switched off (the default) the assistant keeps working with its rules and says honestly what is unavailable.</summary>
[Collection(ApiCollection.Name)]
public class AssistantWithoutAiTests(ApiFactory factory)
{
    [Fact]
    public async Task A_sentence_the_rules_miss_is_simply_not_understood_and_nothing_is_proposed()
    {
        var (client, _) = await factory.NewUserAsync();

        var reply = await client.PostAsJsonAsync("/api/v1/agent/messages", new { text = "ahora quiero algo diferente de lo anterior" });
        var body = await reply.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("understood").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("proposal").ValueKind);
    }

    [Fact]
    public async Task Application_prep_says_it_is_unavailable_and_refuses_to_generate()
    {
        var (client, _) = await factory.NewUserAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var jobId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().JobOffers.Where(j => j.IsActive).Select(j => j.Id).FirstAsync();

        var preview = await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{jobId}/application-prep");
        var generate = await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/application-prep", new { approved = true });

        Assert.False(preview.GetProperty("available").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, preview.GetProperty("unavailableReason").ValueKind);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, generate.StatusCode);
    }
}
