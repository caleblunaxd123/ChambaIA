using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ChambaIA.Api.Endpoints;
using ChambaIA.Infrastructure;
using ChambaIA.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace ChambaIA.Api.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddApiJson(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });
        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured from IOptions<JwtOptions> so the final configuration (env vars, test overrides) is what validates tokens.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Key)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name"
                };
            });

        services.AddAuthorization(o => o.AddPolicy(AdminEndpoints.Policy, p => p.RequireAuthenticatedUser().AddRequirements(new AdminRequirement())));
        services.AddSingleton<IAuthorizationHandler, AdminAuthorizationHandler>();
        return services;
    }

    /// <summary>Global per-IP limiter plus a much tighter one for credential endpoints (brute-force protection).</summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        var authPerMinute = config.GetValue("RateLimiting:AuthPerMinute", 10);
        var uploadsPerMinute = config.GetValue("RateLimiting:UploadsPerMinute", 6);
        var globalPerMinute = config.GetValue("RateLimiting:GlobalPerMinute", 300);

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetFixedWindowLimiter(Client(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = globalPerMinute,
                    Window = TimeSpan.FromMinutes(1)
                }));
            o.AddPolicy(ResumeEndpoints.UploadRateLimitPolicy, ctx =>
                RateLimitPartition.GetFixedWindowLimiter("upload:" + Client(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = uploadsPerMinute,
                    Window = TimeSpan.FromMinutes(1)
                }));
            o.AddPolicy(AuthEndpoints.RateLimitPolicy, ctx =>
                RateLimitPartition.GetFixedWindowLimiter("auth:" + Client(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPerMinute,
                    Window = TimeSpan.FromMinutes(1)
                }));
        });
        return services;
    }

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services, IConfiguration config)
    {
        var checks = services.AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
            .AddNpgSql(
                sp => sp.GetRequiredService<IConfiguration>().GetConnectionString(DependencyInjection.PostgresConnection)!,
                name: "postgres", tags: ["ready"]);

        var redis = config.GetConnectionString(DependencyInjection.RedisConnection);
        if (!string.IsNullOrWhiteSpace(redis))
            checks.AddRedis(redis, "redis", tags: ["ready"]);

        return services;
    }

    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
        {
            ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
            if (ctx.HttpContext.Items[CorrelationIdMiddleware.ItemKey] is string correlationId)
                ctx.ProblemDetails.Extensions["correlationId"] = correlationId;
        });
        return services;
    }

    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ChambaIA API",
                Version = "v1",
                Description = "Tu agente personal para encontrar trabajo."
            });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Pega el access token devuelto por /auth/login."
            });
            o.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });
        return services;
    }

    private static string Client(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
