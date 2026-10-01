using ChambaIA.Api.Endpoints;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Infrastructure;
using ChambaIA.Infrastructure.Persistence;
using ChambaIA.Infrastructure.Seeding;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

DevelopmentSecrets.EnsureJwtKey(builder);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));

builder.Services
    .AddInfrastructure(builder.Configuration)
    .AddIdentityCore()
    .AddAuthServices(builder.Configuration)
    .AddApiJson()
    .AddJwtAuthentication()
    .AddApiRateLimiting(builder.Configuration)
    .AddApiProblemDetails()
    .AddApiHealthChecks(builder.Configuration)
    .AddApiSwagger();

builder.Services.AddCors(o => o.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    if (builder.Environment.IsDevelopment() || origins.Length == 0)
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    else
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "ChambaIA API v1");
        o.DocumentTitle = "ChambaIA API";
    });
}

var api = app.MapGroup("/api/v1");
api.MapAuth();
api.MapProfile();
api.MapPreferences();
api.MapJobs();
api.MapMatches();
api.MapApplications();
api.MapCatalog();
api.MapResumes();
api.MapAgent();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = c => c.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

await Bootstrap.RunAsync(app);

public static class Bootstrap
{
    /// <summary>Applies migrations (opt-out in production) and the demo seed (opt-in), then starts the host.</summary>
    public static async Task RunAsync(WebApplication app)
    {
        var migrate = app.Configuration.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment());
        if (migrate)
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
        }

        await app.RunAsync();
    }
}

public partial class Program;
