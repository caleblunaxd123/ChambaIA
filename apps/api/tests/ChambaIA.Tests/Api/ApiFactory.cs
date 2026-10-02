using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace ChambaIA.Tests.Api;

/// <summary>Boots the real API against a throwaway PostgreSQL (pgvector) container. Shared by all integration tests.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoEmail = "areli.demo@chambaia.dev";
    public const string DemoPassword = "Demo12345-tests";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string ResumesDir { get; } = Path.Combine(Path.GetTempPath(), "chambaia-tests-" + Guid.NewGuid().ToString("N"));

    public Task InitializeAsync() => _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        if (Directory.Exists(ResumesDir)) Directory.Delete(ResumesDir, recursive: true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Key", "integration-tests-signing-key-0123456789-abcdef");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:DemoEmail", DemoEmail);
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
        builder.UseSetting("Storage:ResumesPath", ResumesDir);
        builder.UseSetting("RateLimiting:UploadsPerMinute", "10000");
        builder.UseSetting("RateLimiting:AuthPerMinute", "10000");
        builder.UseSetting("RateLimiting:GlobalPerMinute", "100000");
        ConfigureExtra(builder);
    }

    /// <summary>Hook for factories that need extra settings or service replacements.</summary>
    protected virtual void ConfigureExtra(IWebHostBuilder builder) { }

    /// <summary>Registers a brand new user and returns an authenticated client for it.</summary>
    public async Task<(HttpClient Client, AuthBody Auth)> NewUserAsync(string? name = null)
    {
        var client = CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "Segura12345", fullName = name ?? "Usuaria Test" });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthBody>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    public async Task<HttpClient> DemoClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = DemoEmail, password = DemoPassword });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthBody>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public sealed record AuthBody(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, UserBody User);

    public sealed record UserBody(Guid Id, string Email, string FullName, string Plan);
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
