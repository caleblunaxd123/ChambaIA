using ChambaIA.Infrastructure.Auth;
using ChambaIA.Infrastructure.Embeddings;
using Microsoft.Extensions.Options;
using ChambaIA.Infrastructure.Identity;
using ChambaIA.Infrastructure.Ingestion;
using ChambaIA.Infrastructure.Matching;
using ChambaIA.Infrastructure.Notifications;
using ChambaIA.Infrastructure.Persistence;
using ChambaIA.Infrastructure.Resumes;
using ChambaIA.Infrastructure.Seeding;
using ChambaIA.Infrastructure.Tracking;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChambaIA.Infrastructure;

public static class DependencyInjection
{
    public const string PostgresConnection = "Postgres";
    public const string RedisConnection = "Redis";

    /// <summary>Everything shared by the API and the Worker: database, Identity stores, cache, matching.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Resolved lazily so the final configuration (env vars, test overrides) is what the DbContext sees.
        services.AddDbContext<AppDbContext>((sp, o) =>
        {
            var postgres = sp.GetRequiredService<IConfiguration>().GetConnectionString(PostgresConnection)
                ?? throw new InvalidOperationException($"ConnectionStrings:{PostgresConnection} is not configured.");
            o.UseNpgsql(postgres, npgsql => npgsql.UseVector());
        });

        services.AddSingleton(TimeProvider.System);

        var redis = config.GetConnectionString(RedisConnection);
        if (string.IsNullOrWhiteSpace(redis))
            services.AddDistributedMemoryCache();
        else
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = "chambaia:";
            });

        AddEmbeddings(services, config);
        AddPush(services, config);
        services.AddScoped<MatchRecomputeService>();
        services.AddScoped<TrackerService>();

        services.Configure<ResumeStorageOptions>(config.GetSection(ResumeStorageOptions.Section));
        services.AddSingleton<IResumeTextExtractor, ResumeTextExtractor>();
        services.AddSingleton<IResumeStorage, LocalResumeStorage>();
        services.AddScoped<ResumeService>();

        services.Configure<SeedOptions>(config.GetSection(SeedOptions.Section));

        AddIngestion(services, config);

        return services;
    }

    /// <summary>
    /// Sources are registered from configuration (Ingestion:IncludeDemo, Ingestion:Feeds). Feed connectors are singletons
    /// so their ETag cache survives between runs; the service itself is scoped (it owns a DbContext).
    /// </summary>
    /// <summary>
    /// Stage C of the pipeline. With Embeddings:Provider = None (the default) the product runs exactly as before; with Ollama
    /// each offer and each candidate gets a 1024-dimension vector that pgvector compares inside PostgreSQL.
    /// </summary>
    private static void AddEmbeddings(IServiceCollection services, IConfiguration config)
    {
        services.Configure<EmbeddingOptions>(config.GetSection(EmbeddingOptions.Section));
        services.AddSingleton<EmbeddingCircuit>();
        services.AddTransient<NullEmbeddingProvider>();
        services.AddHttpClient<OllamaEmbeddingProvider>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, o.TimeoutSeconds));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ChambaIA-Embeddings/1.0");
        });
        services.AddScoped<IEmbeddingProvider>(sp => sp.GetRequiredService<IOptions<EmbeddingOptions>>().Value.Enabled
            ? sp.GetRequiredService<OllamaEmbeddingProvider>()
            : sp.GetRequiredService<NullEmbeddingProvider>());
        services.AddScoped<EmbeddingService>();
    }

    /// <summary>Alerts: the digest service always runs; actual push delivery happens only with Push:Provider = Expo.</summary>
    private static void AddPush(IServiceCollection services, IConfiguration config)
    {
        services.Configure<PushOptions>(config.GetSection(PushOptions.Section));
        services.AddTransient<NullPushSender>();
        services.AddHttpClient<ExpoPushSender>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<PushOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ChambaIA-Push/1.0");
        });
        services.AddScoped<IPushSender>(sp => sp.GetRequiredService<IOptions<PushOptions>>().Value.Enabled
            ? sp.GetRequiredService<ExpoPushSender>()
            : sp.GetRequiredService<NullPushSender>());
        services.AddScoped<DigestService>();
    }

    private static void AddIngestion(IServiceCollection services, IConfiguration config)
    {
        services.Configure<IngestionOptions>(config.GetSection(IngestionOptions.Section));
        var ingestion = config.GetSection(IngestionOptions.Section).Get<IngestionOptions>() ?? new IngestionOptions();

        services.AddHttpClient(JsonFeedSource.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("ChambaIA-FeedReader/1.0 (+https://chambaia.dev/fuentes)");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddSingleton<DemoJobSource>();
        if (ingestion.IncludeDemo) services.AddSingleton<IJobSource>(sp => sp.GetRequiredService<DemoJobSource>());

        foreach (var feed in ingestion.Feeds.Where(f => f.Enabled && !string.IsNullOrWhiteSpace(f.Key) && !string.IsNullOrWhiteSpace(f.Url)))
        {
            services.AddSingleton<IJobSource>(sp => new JsonFeedSource(
                feed,
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JsonFeedSource>>()));
        }

        services.AddScoped<IngestionService>();
    }

    /// <summary>Identity with the policy used by the whole product. Only the API needs the web-facing parts.</summary>
    public static IServiceCollection AddIdentityCore(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        // The seeder creates the demo user, so it lives with Identity (the worker has no UserManager and must not
        // see it: Development validates every registration at startup and would refuse to boot).
        services.AddScoped<DemoDataSeeder>();

        return services;
    }

    public static IServiceCollection AddAuthServices(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<AuthService>();
        return services;
    }
}
