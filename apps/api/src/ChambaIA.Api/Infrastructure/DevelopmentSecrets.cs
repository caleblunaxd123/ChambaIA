using System.Security.Cryptography;

namespace ChambaIA.Api.Infrastructure;

/// <summary>
/// Zero-setup local development without committing any secret: when no Jwt:Key is configured in Development,
/// a random key is generated once into the git-ignored <c>.dev/jwt.key</c> file and reused on later runs.
/// Any other environment must provide Jwt__Key explicitly.
/// </summary>
public static class DevelopmentSecrets
{
    public static void EnsureJwtKey(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment() || !string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"])) return;

        var directory = Path.Combine(builder.Environment.ContentRootPath, ".dev");
        var file = Path.Combine(directory, "jwt.key");

        if (!File.Exists(file))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(file, Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        }

        builder.Configuration["Jwt:Key"] = File.ReadAllText(file).Trim();
    }
}
