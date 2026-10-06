using ChambaIA.Infrastructure.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ChambaIA.Api.Endpoints;

/// <summary>Who may open the operator dashboards. Emails come from configuration (<c>Admin__Emails__0</c>...), never from code.</summary>
public sealed class AdminOptions
{
    public const string Section = "Admin";
    public string[] Emails { get; set; } = [];
}

public sealed class AdminRequirement : IAuthorizationRequirement;

/// <summary>
/// Succeeds for signed-in users whose email is on the allow-list. The allow-list is only meaningful once that account exists
/// (registration rejects an already-registered email), so create the admin account first, then list it.
/// </summary>
public sealed class AdminAuthorizationHandler(IOptions<AdminOptions> options) : AuthorizationHandler<AdminRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        var email = context.User.FindFirst("email")?.Value;
        if (!string.IsNullOrWhiteSpace(email) && options.Value.Emails.Contains(email, StringComparer.OrdinalIgnoreCase))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public static class AdminEndpoints
{
    public const string Policy = "Admin";

    public static RouteGroupBuilder MapAdmin(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").WithTags("Admin").RequireAuthorization(Policy);

        // Real spend from the AiUsage table: totals, by provider and operation, by day, budget left, provider health.
        admin.MapGet("/ai/usage", async (int? days, AiUsageService usage, CancellationToken ct) =>
            Results.Ok(await usage.ReportAsync(days ?? 30, ct)));

        return api;
    }
}
