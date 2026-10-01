using System.ComponentModel.DataAnnotations;

namespace ChambaIA.Api.Contracts;

public sealed class RegisterRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = "";

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = "";

    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; init; } = "";
}

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = "";

    [Required, StringLength(100)]
    public string Password { get; init; } = "";
}

public sealed class RefreshRequest
{
    [Required, StringLength(200)]
    public string RefreshToken { get; init; } = "";
}

public sealed class DeleteAccountRequest
{
    [Required, StringLength(100)]
    public string Password { get; init; } = "";
}

public sealed record UserDto(Guid Id, string Email, string FullName, string Plan);

public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, UserDto User);
