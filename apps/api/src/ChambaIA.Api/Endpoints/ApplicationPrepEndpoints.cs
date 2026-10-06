using ChambaIA.Api.Contracts;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Infrastructure.Ai;

namespace ChambaIA.Api.Endpoints;

public static class ApplicationPrepEndpoints
{
    public static RouteGroupBuilder MapApplicationPrep(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/jobs/{jobId:guid}/application-prep").WithTags("Application prep").RequireAuthorization();

        // Step 1: nothing is sent anywhere. Shows the exact text that would go to the model, what never does, and the quota left.
        group.MapGet("", async (Guid jobId, HttpContext http, ApplicationPrepService prep, CancellationToken ct) =>
        {
            var preview = await prep.PreviewAsync(http.User.GetUserId(), jobId, ct);
            return preview is null
                ? Results.NotFound()
                : Results.Ok(new ApplicationPrepPreviewDto(
                    preview.Available, preview.UnavailableReason, preview.Payload, preview.Includes, preview.Excludes,
                    new AiQuotaDto(preview.Quota.CallsToday, preview.Quota.DailyLimit, preview.Quota.RemainingToday)));
        });

        // Step 2: only with an explicit approval. The draft is returned to the person and stored nowhere.
        group.MapPost("", async (Guid jobId, ApplicationPrepRequest request, HttpContext http, ApplicationPrepService prep, CancellationToken ct) =>
            {
                if (!request.Approved)
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["approved"] = ["Debes aprobar el envío de los datos mostrados para generar el borrador."] });

                var result = await prep.GenerateAsync(http.User.GetUserId(), jobId, request.Tone == "formal", ct);
                return result.Outcome switch
                {
                    PrepOutcome.Ok => Results.Ok(new ApplicationPrepDraftDto(
                        result.Draft!.Message, result.Draft.Highlights, result.Draft.Gaps, result.Draft.Questions, result.Draft.Warnings)),
                    PrepOutcome.NotFound => Results.NotFound(),
                    PrepOutcome.QuotaReached => Results.Problem("Ya usaste tus borradores de hoy. Vuelve mañana.", statusCode: StatusCodes.Status429TooManyRequests, title: "Límite diario alcanzado"),
                    PrepOutcome.Unreliable => Results.Problem("No pudimos generar un borrador confiable esta vez. Inténtalo de nuevo.", statusCode: StatusCodes.Status502BadGateway, title: "Borrador no confiable"),
                    _ => Results.Problem("La preparación con IA no está disponible por ahora.", statusCode: StatusCodes.Status503ServiceUnavailable, title: "IA no disponible")
                };
            })
            .Validate<ApplicationPrepRequest>();

        return api;
    }
}
