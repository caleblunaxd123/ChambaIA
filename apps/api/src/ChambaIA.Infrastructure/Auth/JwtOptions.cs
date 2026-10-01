using System.ComponentModel.DataAnnotations;

namespace ChambaIA.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    /// <summary>HMAC signing key. Always comes from the environment (Jwt__Key); never committed.</summary>
    [Required, MinLength(32)]
    public string Key { get; set; } = "";

    [Required]
    public string Issuer { get; set; } = "chambaia";

    [Required]
    public string Audience { get; set; } = "chambaia-mobile";

    [Range(1, 240)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 180)]
    public int RefreshTokenDays { get; set; } = 30;
}
