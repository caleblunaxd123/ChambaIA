using System.Security.Claims;

namespace ChambaIA.Api.Infrastructure;

public static class CurrentUser
{
    /// <summary>The authenticated user's id (JWT "sub"). Every private endpoint scopes its queries with this value.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : throw new UnauthorizedAccessException("Missing user id claim.");
    }
}
