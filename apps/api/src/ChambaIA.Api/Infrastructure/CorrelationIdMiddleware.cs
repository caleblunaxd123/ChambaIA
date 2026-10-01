using Serilog.Context;

namespace ChambaIA.Api.Infrastructure;

/// <summary>Accepts or creates an X-Correlation-ID, echoes it back and attaches it to every log line of the request.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsAcceptable(incoming) ? incoming! : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
            await next(context);
    }

    // Never trust client input blindly: it ends up in logs and headers.
    private static bool IsAcceptable(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
}
