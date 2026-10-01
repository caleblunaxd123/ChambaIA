using ChambaIA.Domain.Entities;
using ChambaIA.Infrastructure.Identity;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChambaIA.Infrastructure.Auth;

public sealed record AuthUser(Guid Id, string Email, string FullName, string Plan);

public sealed record AuthTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, AuthUser User);

public enum AuthError { None, EmailTaken, InvalidCredentials, LockedOut, InvalidRefreshToken, WeakPassword }

public sealed record AuthResult(AuthTokens? Tokens, AuthError Error, IReadOnlyList<string>? Details = null)
{
    public bool Succeeded => Tokens is not null;
    public static AuthResult Ok(AuthTokens tokens) => new(tokens, AuthError.None);
    public static AuthResult Fail(AuthError error, IReadOnlyList<string>? details = null) => new(null, error, details);
}

/// <summary>Registration, login and refresh-token rotation. Raw refresh tokens are never persisted.</summary>
public sealed class AuthService(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    ITokenService tokens,
    IOptions<JwtOptions> jwtOptions,
    ChambaIA.Infrastructure.Resumes.IResumeStorage storage,
    TimeProvider clock)
{
    public async Task<AuthResult> RegisterAsync(string email, string password, string fullName, CancellationToken ct)
    {
        if (await users.FindByEmailAsync(email) is not null)
            return AuthResult.Fail(AuthError.EmailTaken);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName.Trim(),
            CreatedAt = clock.GetUtcNow()
        };

        // User + empty profile + default preferences are created atomically.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            return AuthResult.Fail(
                created.Errors.Any(e => e.Code.StartsWith("Password")) ? AuthError.WeakPassword : AuthError.EmailTaken,
                created.Errors.Select(e => e.Description).ToList());

        db.CandidateProfiles.Add(new CandidateProfile { UserId = user.Id });
        db.JobPreferences.Add(new JobPreferences { UserId = user.Id });
        await db.SaveChangesAsync(ct);

        var tokensIssued = await IssueAsync(user, ct);
        await tx.CommitAsync(ct);
        return AuthResult.Ok(tokensIssued);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        // Same answer for "unknown email" and "wrong password" so the endpoint cannot be used to enumerate accounts.
        if (user is null) return AuthResult.Fail(AuthError.InvalidCredentials);

        if (await users.IsLockedOutAsync(user)) return AuthResult.Fail(AuthError.LockedOut);

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return AuthResult.Fail(AuthError.InvalidCredentials);
        }

        await users.ResetAccessFailedCountAsync(user);
        return AuthResult.Ok(await IssueAsync(user, ct));
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = tokens.Hash(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return AuthResult.Fail(AuthError.InvalidRefreshToken);

        var now = clock.GetUtcNow();
        if (stored.RevokedAt is not null)
        {
            // A rotated token was presented again: assume theft and kill every session of this user.
            await RevokeAllAsync(stored.UserId, now, ct);
            return AuthResult.Fail(AuthError.InvalidRefreshToken);
        }

        if (!stored.IsActive(now)) return AuthResult.Fail(AuthError.InvalidRefreshToken);

        var user = await users.FindByIdAsync(stored.UserId.ToString());
        if (user is null) return AuthResult.Fail(AuthError.InvalidRefreshToken);

        var result = await IssueAsync(user, ct, save: false);
        stored.RevokedAt = now;
        stored.ReplacedByTokenHash = tokens.Hash(result.RefreshToken);
        await db.SaveChangesAsync(ct);

        return AuthResult.Ok(result);
    }

    /// <summary>
    /// Permanently deletes the account and everything that hangs from it (profile, preferences, matches, applications,
    /// resumes, refresh tokens via cascading FKs; AI usage rows explicitly). Requires the current password.
    /// </summary>
    public async Task<bool> DeleteAccountAsync(Guid userId, string password, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !await users.CheckPasswordAsync(user, password)) return false;

        var cvKeys = await db.Resumes.Where(r => r.UserId == userId).Select(r => r.FileUrl).ToListAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.AiUsages.Where(u => u.UserId == userId).ExecuteDeleteAsync(ct);
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded) return false;
        await tx.CommitAsync(ct);
        foreach (var key in cvKeys) await storage.DeleteAsync(key);
        return true;
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct)
    {
        var hash = tokens.Hash(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthTokens> IssueAsync(ApplicationUser user, CancellationToken ct, bool save = true)
    {
        var access = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.Hash(refresh),
            CreatedAt = clock.GetUtcNow(),
            ExpiresAt = clock.GetUtcNow().AddDays(jwtOptions.Value.RefreshTokenDays)
        });
        if (save) await db.SaveChangesAsync(ct);

        return new AuthTokens(access.Value, access.ExpiresAt, refresh,
            new AuthUser(user.Id, user.Email ?? "", user.FullName, user.Plan.ToString()));
    }

    private Task RevokeAllAsync(Guid userId, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
}
