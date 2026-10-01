using ChambaIA.Api.Contracts;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Domain.Agent;
using ChambaIA.Domain.Geo;
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

        // Phase 1 slice of the assistant: rules only (no LLM, no tokens). The LLM fallback for unrecognised sentences lands in phase 9.
        group.MapPost("/messages", async (AgentMessageRequest request, HttpContext http, AppDbContext db, MatchRecomputeService matcher, TimeProvider clock, CancellationToken ct) =>
            {
                var userId = http.User.GetUserId();
                var prefs = await db.JobPreferences.SingleOrDefaultAsync(p => p.UserId == userId, ct);
                var candidateId = await db.CandidateProfiles.Where(p => p.UserId == userId).Select(p => (Guid?)p.Id).SingleOrDefaultAsync(ct);
                if (prefs is null || candidateId is null) return Results.NotFound();

                var commands = AgentCommandParser.Parse(request.Text);
                if (commands.Count == 0)
                    return Results.Ok(new AgentReplyDto("No logré entender eso todavía. " + Hint, false, [], null));

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

                var overview = await MatchOverviewQuery.GetAsync(db, candidateId.Value, clock.GetUtcNow(), ct);
                var visible = overview.Strong + overview.Possible + overview.Review;
                var reply = $"Listo. {string.Join(' ', changes)} Ahora tienes {visible} {(visible == 1 ? "oportunidad" : "oportunidades")} por revisar.";
                return Results.Ok(new AgentReplyDto(reply, true, changes, overview));
            })
            .Validate<AgentMessageRequest>();

        return api;
    }
}
