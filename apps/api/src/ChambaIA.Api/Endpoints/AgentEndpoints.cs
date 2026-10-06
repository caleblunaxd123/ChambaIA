using ChambaIA.Api.Contracts;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Domain.Agent;
using ChambaIA.Domain.Ai;
using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Geo;
using ChambaIA.Infrastructure.Ai;
using ChambaIA.Infrastructure.Matching;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Api.Endpoints;

public static class AgentEndpoints
{
    private const string Hint =
        "Prueba con: «mínimo 1800 soles», «no me muestres trabajos en Ate», «busca también facturación», «máximo una hora de viaje», «solo lunes a viernes» o «no quiero call center».";

    public static RouteGroupBuilder MapAgent(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/agent").WithTags("Agent").RequireAuthorization();

        // The rules always run first: zero cost, zero latency, applied at once. Only when they understand nothing, and only if AI is
        // switched on, does a small model propose an interpretation, which is NOT applied until the person approves it.
        group.MapPost("/messages", async (AgentMessageRequest request, HttpContext http, AppDbContext db, MatchRecomputeService matcher, AiRouter ai, TimeProvider clock, CancellationToken ct) =>
            {
                var userId = http.User.GetUserId();
                var (prefs, candidateId) = await LoadAsync(db, userId, ct);
                if (prefs is null || candidateId is null) return Results.NotFound();

                var commands = AgentCommandParser.Parse(request.Text);
                if (commands.Count > 0) return await ApplyAsync(commands, prefs, candidateId.Value, userId, db, matcher, clock, ct);

                if (!ai.CanRoute(AiTask.CommandFallback)) return NotUnderstood();

                var plan = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Plan).SingleAsync(ct);
                var answer = await ai.CompleteAsync(AiTask.CommandFallback,
                    new AiCall(userId, plan, AgentLlmPrompt.System, AgentLlmPrompt.User(request.Text), MaxOutputTokens: 300), ct);

                if (answer.Reason == AiUnavailable.BudgetReached)
                    return Results.Ok(new AgentReplyDto("Por hoy ya usé mi ayuda extra para entender frases libres. " + Hint, false, [], null));
                if (!answer.Ok) return NotUnderstood();

                var proposed = AgentLlmParser.Parse(answer.Text);
                if (proposed.Count == 0) return NotUnderstood();

                var descriptions = AgentCommandApplier.Preview(prefs, proposed);
                if (descriptions.Count == 0) return Results.Ok(new AgentReplyDto("Eso ya lo tenía anotado. No cambié nada.", true, [], null));

                var proposal = new AgentProposalDto(proposed.Select(ToDto).ToList(), descriptions);
                return Results.Ok(new AgentReplyDto($"Entendí esto: {string.Join(" ", descriptions)} ¿Lo aplico?", true, [], null, proposal));
            })
            .Validate<AgentMessageRequest>();

        // Approval of a proposal. The commands come back from the app, so they are validated again exactly like a model's output.
        group.MapPost("/apply", async (ApplyAgentCommandsRequest request, HttpContext http, AppDbContext db, MatchRecomputeService matcher, TimeProvider clock, CancellationToken ct) =>
            {
                var userId = http.User.GetUserId();
                var (prefs, candidateId) = await LoadAsync(db, userId, ct);
                if (prefs is null || candidateId is null) return Results.NotFound();

                var commands = AgentCommandValidator.Validate(request.Commands.Select(c => new AgentCommandInput(c.Intent, c.Text, c.Number, c.Modality, c.Flag)));
                if (commands.Count == 0)
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["commands"] = ["Ninguno de los cambios es válido."] });

                return await ApplyAsync(commands, prefs, candidateId.Value, userId, db, matcher, clock, ct);
            })
            .Validate<ApplyAgentCommandsRequest>();

        return api;
    }

    private static async Task<(JobPreferences? Prefs, Guid? CandidateId)> LoadAsync(AppDbContext db, Guid userId, CancellationToken ct) =>
        (await db.JobPreferences.SingleOrDefaultAsync(p => p.UserId == userId, ct),
         await db.CandidateProfiles.Where(p => p.UserId == userId).Select(p => (Guid?)p.Id).SingleOrDefaultAsync(ct));

    private static IResult NotUnderstood() => Results.Ok(new AgentReplyDto("No logré entender eso todavía. " + Hint, false, [], null));

    private static AgentCommandDto ToDto(AgentCommand c) => new(c.Intent.ToString(), c.Text, c.Number, c.Modality?.ToString(), c.Flag);

    private static async Task<IResult> ApplyAsync(
        IReadOnlyList<AgentCommand> commands, JobPreferences prefs, Guid candidateId, Guid userId,
        AppDbContext db, MatchRecomputeService matcher, TimeProvider clock, CancellationToken ct)
    {
        var changes = commands
            .Select(c => AgentCommandApplier.Apply(prefs, c))
            .Where(summary => summary is not null)
            .Select(summary => summary!)
            .ToList();

        if (changes.Count == 0)
            return Results.Ok(new AgentReplyDto("Eso ya lo tenía anotado. No cambié nada.", true, [], null));

        // Same rule as PUT /preferences: new wishes re-evaluate the feed immediately.
        prefs.HomeDistrict = prefs.HomeDistrict is null ? null : LimaDistricts.Canonical(prefs.HomeDistrict);
        await db.SaveChangesAsync(ct);
        await matcher.RecomputeForUserAsync(userId, ct);

        var overview = await MatchOverviewQuery.GetAsync(db, candidateId, clock.GetUtcNow(), ct);
        var visible = overview.Strong + overview.Possible + overview.Review;
        var reply = $"Listo. {string.Join(' ', changes)} Ahora tienes {visible} {(visible == 1 ? "oportunidad" : "oportunidades")} por revisar.";
        return Results.Ok(new AgentReplyDto(reply, true, changes, overview));
    }
}
