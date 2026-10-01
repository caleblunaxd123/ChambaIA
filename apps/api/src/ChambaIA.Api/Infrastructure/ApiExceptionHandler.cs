using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ChambaIA.Api.Infrastructure;

/// <summary>Turns client mistakes into 4xx ProblemDetails instead of 500s; anything else stays a 500 without leaking details.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            BadHttpRequestException bad => (bad.StatusCode, "Solicitud inválida"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autorizado"),
            _ => (StatusCodes.Status500InternalServerError, "Error inesperado")
        };

        if (status >= 500) logger.LogError(exception, "Unhandled exception");
        else logger.LogInformation("Request rejected: {Message}", exception.Message);

        http.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status >= 500 ? "Ocurrió un error en el servidor. Inténtalo de nuevo." : exception.Message
            }
        });
    }
}
