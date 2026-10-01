using ChambaIA.Api.Contracts;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Api.Endpoints;

public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static RouteGroupBuilder MapAuth(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Auth").AllowAnonymous().RequireRateLimiting(RateLimitPolicy);

        group.MapPost("/register", async (RegisterRequest request, AuthService auth, CancellationToken ct) =>
                ToResult(await auth.RegisterAsync(request.Email.Trim(), request.Password, request.FullName, ct), created: true))
            .Validate<RegisterRequest>();

        group.MapPost("/login", async (LoginRequest request, AuthService auth, CancellationToken ct) =>
                ToResult(await auth.LoginAsync(request.Email.Trim(), request.Password, ct)))
            .Validate<LoginRequest>();

        group.MapPost("/refresh", async (RefreshRequest request, AuthService auth, CancellationToken ct) =>
                ToResult(await auth.RefreshAsync(request.RefreshToken, ct)))
            .Validate<RefreshRequest>();

        group.MapPost("/logout", async (RefreshRequest request, HttpContext http, AuthService auth, CancellationToken ct) =>
            {
                await auth.LogoutAsync(http.User.GetUserId(), request.RefreshToken, ct);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .Validate<RefreshRequest>();

        // Right to erasure, short of deleting the account: forget every reaction (saved/dismissed/applied) and the tracker.
        api.MapPost("/account/clear-history", async (HttpContext http, ChambaIA.Infrastructure.Persistence.AppDbContext db, ChambaIA.Infrastructure.Matching.MatchRecomputeService matcher, CancellationToken ct) =>
            {
                var userId = http.User.GetUserId();
                await db.Applications.Where(a => a.UserId == userId).ExecuteDeleteAsync(ct);
                await db.Matches.Where(m => db.CandidateProfiles.Any(p => p.Id == m.CandidateId && p.UserId == userId)).ExecuteDeleteAsync(ct);
                await matcher.RecomputeForUserAsync(userId, ct);
                return Results.NoContent();
            })
            .WithTags("Account")
            .RequireAuthorization();

        api.MapPost("/account/delete", async (DeleteAccountRequest request, HttpContext http, AuthService auth, CancellationToken ct) =>
                await auth.DeleteAccountAsync(http.User.GetUserId(), request.Password, ct)
                    ? Results.NoContent()
                    : Results.Problem("La contraseña no es correcta.", statusCode: StatusCodes.Status403Forbidden, title: "No se pudo eliminar la cuenta"))
            .WithTags("Account")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicy)
            .Validate<DeleteAccountRequest>();

        return api;
    }

    private static IResult ToResult(AuthResult result, bool created = false)
    {
        if (result.Tokens is { } t)
        {
            var body = new AuthResponse(t.AccessToken, t.AccessTokenExpiresAt, t.RefreshToken,
                new UserDto(t.User.Id, t.User.Email, t.User.FullName, t.User.Plan));
            return created ? Results.Created("/api/v1/profile", body) : Results.Ok(body);
        }

        return result.Error switch
        {
            AuthError.EmailTaken => Results.Problem("Ya existe una cuenta con ese correo.", statusCode: StatusCodes.Status409Conflict, title: "Correo en uso"),
            AuthError.WeakPassword => Results.ValidationProblem(
                new Dictionary<string, string[]> { ["password"] = [.. result.Details ?? ["Contraseña muy débil."]] }),
            AuthError.LockedOut => Results.Problem("Demasiados intentos fallidos. Inténtalo de nuevo en unos minutos.", statusCode: StatusCodes.Status423Locked, title: "Cuenta bloqueada temporalmente"),
            AuthError.InvalidRefreshToken => Results.Problem("La sesión expiró. Inicia sesión de nuevo.", statusCode: StatusCodes.Status401Unauthorized, title: "Sesión inválida"),
            _ => Results.Problem("Correo o contraseña incorrectos.", statusCode: StatusCodes.Status401Unauthorized, title: "Credenciales inválidas")
        };
    }
}
