using ChambaIA.Infrastructure.Auth;
using ChambaIA.Infrastructure.Identity;
using ChambaIA.Infrastructure.Matching;
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

        services.AddScoped<MatchRecomputeService>();
        services.AddScoped<TrackerService>();

        services.Configure<ResumeStorageOptions>(config.GetSection(ResumeStorageOptions.Section));
        services.AddSingleton<IResumeTextExtractor, ResumeTextExtractor>();
        services.AddSingleton<IResumeStorage, LocalResumeStorage>();
        services.AddScoped<ResumeService>();

        services.Configure<SeedOptions>(config.GetSection(SeedOptions.Section));
        services.AddScoped<DemoDataSeeder>();

        return services;
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
