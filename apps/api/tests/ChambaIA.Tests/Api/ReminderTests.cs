using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChambaIA.Infrastructure.Notifications;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using static ChambaIA.Tests.Api.PushTestKit;

namespace ChambaIA.Tests.Api;

/// <summary>Phase 7: interview reminders, the follow-up nudge, and the history of a tracker card.</summary>
[Collection(ApiCollection.Name)]
public class ReminderTests(ApiFactory factory)
{
    private async Task<(HttpClient Client, string Token, Guid JobId, string Company, string Title)> UserWithJobAsync(bool push = true)
    {
        var (client, _) = await factory.NewUserAsync();
        (await client.PutAsJsonAsync("/api/v1/preferences", new { pushEnabled = push })).EnsureSuccessStatusCode();
        var token = NewToken();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/devices", new { token, platform = "android" })).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await db.JobOffers.Where(j => j.IsActive).OrderBy(j => j.Id).Skip(Random.Shared.Next(0, 20)).Select(j => new { j.Id, j.Company, j.Title }).FirstAsync();
        return (client, token, job.Id, job.Company, job.Title);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<Guid> CreateCard(HttpClient client, Guid jobId, string status) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/applications", new { jobId, status }))).GetProperty("id").GetGuid();

    private async Task<ReminderReport> Remind(FakePush push, DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = new ReminderService(scope.ServiceProvider.GetRequiredService<AppDbContext>(), push, Options.Create(new PushOptions { Provider = "Expo" }),
            new FixedClock(now), NullLogger<ReminderService>.Instance);
        return await service.RunAsync();
    }

    private static Task<HttpResponseMessage> Schedule(HttpClient client, Guid id, DateTimeOffset interview) =>
        client.PatchAsJsonAsync($"/api/v1/applications/{id}", new { status = "interview", interviewDate = interview });

    // ---- interview reminders -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_day_before_an_interview_the_user_gets_one_reminder_that_opens_the_job()
    {
        var (client, token, jobId, company, _) = await UserWithJobAsync();
        var id = await CreateCard(client, jobId, "applied");
        var interview = LimaAt(15, 30, daysAhead: 4);
        Assert.Equal(HttpStatusCode.OK, (await Schedule(client, id, interview)).StatusCode);
        var push = new FakePush();

        await Remind(push, interview.AddHours(-20)); // 19:30 the evening before
        await Remind(push, interview.AddHours(-19)); // and again later: still just one

        var sent = Assert.Single(push.To(token));
        Assert.Equal("Mañana tienes una entrevista", sent.Title);
        Assert.Contains(company, sent.Body);
        Assert.Contains("15:30", sent.Body);
        Assert.Equal(jobId, sent.Data!["jobId"]);
        Assert.Equal("interview", sent.Data["type"]);

        var inbox = await Json(await client.GetAsync("/api/v1/notifications"));
        var item = Assert.Single(inbox.GetProperty("items").EnumerateArray());
        Assert.Equal("interviewDayBefore", item.GetProperty("kind").GetString());
        Assert.Equal(jobId, item.GetProperty("jobId").GetGuid());
    }

    [Fact]
    public async Task Shortly_before_the_interview_a_second_reminder_follows_and_never_repeats()
    {
        var (client, token, jobId, _, _) = await UserWithJobAsync();
        var id = await CreateCard(client, jobId, "applied");
        var interview = LimaAt(11, 0, daysAhead: 4);
        await Schedule(client, id, interview);
        var push = new FakePush();

        await Remind(push, interview.AddHours(-20));
        await Remind(push, interview.AddMinutes(-100));
        await Remind(push, interview.AddMinutes(-40));

        var titles = push.To(token).Select(m => m.Title).ToList();
        Assert.Equal(2, titles.Count);
        Assert.Contains("Mañana tienes una entrevista", titles);
        Assert.Contains("Tu entrevista es en 2 horas", titles);
    }

    [Fact]
    public async Task A_rescheduled_interview_gets_its_reminders_again_for_the_new_date()
    {
        var (client, token, jobId, _, _) = await UserWithJobAsync();
        var id = await CreateCard(client, jobId, "applied");
        var first = LimaAt(15, 0, daysAhead: 4);
        var second = LimaAt(16, 0, daysAhead: 6);
        var push = new FakePush();

        await Schedule(client, id, first);
        await Remind(push, first.AddHours(-20));
        await Schedule(client, id, second);
        await Remind(push, first.AddHours(-19));   // the old date no longer applies
        await Remind(push, second.AddHours(-20));  // the new one does

        Assert.Equal(2, push.To(token).Count);
    }

    [Fact]
    public async Task Reminders_respect_quiet_hours_and_go_out_when_the_morning_comes()
    {
        var (client, token, jobId, _, _) = await UserWithJobAsync();
        var id = await CreateCard(client, jobId, "applied");
        var interview = LimaAt(8, 0, daysAhead: 4);
        await Schedule(client, id, interview);
        var push = new FakePush();

        await Remind(push, interview.AddHours(-9));      // 23:00 the night before
        await Remind(push, interview.AddMinutes(-105));  // 06:15
        Assert.Empty(push.To(token));

        await Remind(push, interview.AddMinutes(-40));   // 07:20
        Assert.Equal("Tu entrevista es en 40 minutos", Assert.Single(push.To(token)).Title);
    }

    [Fact]
    public async Task Users_with_alerts_off_and_cards_given_up_on_get_nothing()
    {
        var (muted, mutedToken, mutedJob, _, _) = await UserWithJobAsync(push: false);
        var (active, activeToken, activeJob, _, _) = await UserWithJobAsync();
        var interview = LimaAt(15, 0, daysAhead: 4);
        await Schedule(muted, await CreateCard(muted, mutedJob, "applied"), interview);
        var discarded = await CreateCard(active, activeJob, "applied");
        await Schedule(active, discarded, interview);
        await active.PatchAsJsonAsync($"/api/v1/applications/{discarded}", new { status = "discarded" });
        var push = new FakePush();

        await Remind(push, interview.AddHours(-20));

        Assert.Empty(push.To(mutedToken));
        Assert.Empty(push.To(activeToken));
    }

    // ---- follow-up ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_application_silent_for_a_week_gets_one_nudge_but_a_fresh_one_does_not()
    {
        var (client, token, jobId, company, _) = await UserWithJobAsync();
        await CreateCard(client, jobId, "applied");
        var push = new FakePush();

        await Remind(push, LimaAt(11, 0, daysAhead: 3));   // 3 days later: too soon
        Assert.Empty(push.To(token));

        await Remind(push, LimaAt(11, 0, daysAhead: 9));   // more than a week later
        await Remind(push, LimaAt(11, 30, daysAhead: 10)); // and the next day: not again

        var sent = Assert.Single(push.To(token));
        Assert.Equal($"¿Novedades de {company}?", sent.Title);
        Assert.Equal("follow-up", sent.Data!["type"]);
    }

    [Fact]
    public async Task Several_silent_applications_produce_one_nudge_per_day_not_a_burst()
    {
        var (client, token, _, _, _) = await UserWithJobAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jobs = await db.JobOffers.Where(j => j.IsActive).OrderBy(j => j.Id).Skip(25).Take(3).Select(j => j.Id).ToListAsync();
            foreach (var job in jobs) await CreateCard(client, job, "applied");
        }
        var push = new FakePush();

        await Remind(push, LimaAt(11, 0, daysAhead: 9));
        await Remind(push, LimaAt(14, 0, daysAhead: 9));
        Assert.Single(push.To(token));

        await Remind(push, LimaAt(11, 0, daysAhead: 10)); // the next day, the next card
        Assert.Equal(2, push.To(token).Count);
    }

    [Fact]
    public async Task The_nudge_never_arrives_at_night()
    {
        var (client, token, jobId, _, _) = await UserWithJobAsync();
        await CreateCard(client, jobId, "applied");
        var push = new FakePush();

        await Remind(push, LimaAt(21, 30, daysAhead: 9));
        await Remind(push, LimaAt(6, 0, daysAhead: 10));

        Assert.Empty(push.To(token));
    }

    // ---- history -----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_history_records_creation_stage_changes_and_interview_dates_but_not_note_edits()
    {
        var (client, _, jobId, _, _) = await UserWithJobAsync();
        var id = await CreateCard(client, jobId, "interested");

        await client.PatchAsJsonAsync($"/api/v1/applications/{id}", new { status = "applied" });
        await client.PatchAsJsonAsync($"/api/v1/applications/{id}", new { notes = "Llamó Recursos Humanos" });   // no event
        await client.PatchAsJsonAsync($"/api/v1/applications/{id}", new { status = "applied" });                  // same stage: no event
        await Schedule(client, id, LimaAt(10, 0, daysAhead: 5));
        await client.PatchAsJsonAsync($"/api/v1/applications/{id}", new { clearInterviewDate = true });

        var events = (await Json(await client.GetAsync($"/api/v1/applications/{id}/history"))).EnumerateArray().ToList();
        var story = events.Select(e => $"{e.GetProperty("kind").GetString()}:{Str(e, "fromStatus")}>{Str(e, "toStatus")}").Reverse().ToList(); // oldest first

        string[] expected =
        [
            "statusChanged:>interested",
            "statusChanged:interested>applied",
            "interviewScheduled:>",
            "statusChanged:applied>interview",
            "interviewCleared:>"
        ];
        // The interview PATCH records the date and the stage change in the same instant, so compare as a set.
        Assert.Equal(expected.Order(), story.Order());
        Assert.Equal("statusChanged:>interested", story[0]);
    }

    private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    [Fact]
    public async Task The_history_is_private_and_disappears_with_the_card()
    {
        var (client, _, jobId, _, _) = await UserWithJobAsync();
        var (other, _) = await factory.NewUserAsync();
        var id = await CreateCard(client, jobId, "interested");

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/applications/{id}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/v1/applications/{id}/history")).StatusCode);

        await client.DeleteAsync($"/api/v1/applications/{id}");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/applications/{id}/history")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().ApplicationEvents.AnyAsync(e => e.ApplicationId == id));
    }

    [Fact]
    public async Task Saving_discarding_and_undoing_from_the_feed_leaves_a_coherent_history()
    {
        var (client, _) = await factory.NewUserAsync();
        (await client.PutAsJsonAsync("/api/v1/profile", new
        {
            fullName = "Usuaria Historial", experienceMonths = 36, educationLevel = "technical", educationStatus = "completed", completeOnboarding = true,
            skills = new[] { new { key = "", name = "Facturación", level = "advanced" }, new { key = "", name = "Excel", level = "intermediate" } }
        })).EnsureSuccessStatusCode();
        var feed = await Json(await client.GetAsync("/api/v1/matches?pageSize=5"));
        var jobId = feed.GetProperty("items")[0].GetProperty("job").GetProperty("id").GetGuid();

        await client.PostAsync($"/api/v1/matches/{jobId}/interested", null);
        var id = (await Json(await client.GetAsync("/api/v1/applications"))).EnumerateArray().Single(a => a.GetProperty("jobId").GetGuid() == jobId).GetProperty("id").GetGuid();
        await client.PatchAsJsonAsync($"/api/v1/applications/{id}", new { notes = "Me interesa por el horario" });
        await client.PostAsync($"/api/v1/matches/{jobId}/dismiss", null);
        await client.PostAsync($"/api/v1/matches/{jobId}/reset", null); // has notes, so the card stays as "found"

        var story = (await Json(await client.GetAsync($"/api/v1/applications/{id}/history"))).EnumerateArray()
            .Select(e => $"{Str(e, "fromStatus")}>{Str(e, "toStatus")}").Reverse().ToList();
        Assert.Equal([">interested", "interested>discarded", "discarded>found"], story);
    }
}
