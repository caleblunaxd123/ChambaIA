using ChambaIA.Domain.Entities;
using ChambaIA.Infrastructure.Identity;
using ChambaIA.Infrastructure.Ingestion;
using ChambaIA.Infrastructure.Matching;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChambaIA.Infrastructure.Seeding;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    public bool Enabled { get; set; }
    public string DemoEmail { get; set; } = "areli.demo@chambaia.dev";
    /// <summary>Provided through configuration/environment; there is no default password in code.</summary>
    public string? DemoPassword { get; set; }
}

/// <summary>
/// Development-only seed: a fictional candidate ("Areli Demo") and fictional offers. Idempotent, and it
/// re-dates the demo offers on every start so the "Nuevas" tab never goes stale. No real people or companies.
/// </summary>
public sealed class DemoDataSeeder(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    MatchRecomputeService matcher,
    IngestionService ingestion,
    DemoJobSource demoSource,
    IOptions<SeedOptions> options,
    TimeProvider clock,
    ILogger<DemoDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!opts.Enabled) return;
        if (string.IsNullOrWhiteSpace(opts.DemoPassword))
        {
            logger.LogWarning("Seed:Enabled is true but Seed:DemoPassword is empty; skipping demo data.");
            return;
        }

        // The demo offers go through the real ingestion pipeline (normaliser + dedup), like any production source.
        await ingestion.RunAsync([demoSource], recompute: false, ct);
        var user = await EnsureDemoUserAsync(opts, ct);
        await matcher.RecomputeForUserAsync(user.Id, ct);

        logger.LogInformation("Demo data ready ({Email}, {Jobs} offers).", opts.DemoEmail, DemoJobs.All.Count);
    }

    private async Task<ApplicationUser> EnsureDemoUserAsync(SeedOptions opts, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(opts.DemoEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = opts.DemoEmail,
                Email = opts.DemoEmail,
                EmailConfirmed = true,
                FullName = "Areli Demo"
            };
            var created = await users.CreateAsync(user, opts.DemoPassword!);
            if (!created.Succeeded)
                throw new InvalidOperationException("Could not create demo user: " + string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        if (!await db.CandidateProfiles.AnyAsync(p => p.UserId == user.Id, ct))
            db.CandidateProfiles.Add(DemoCandidate.Profile(user.Id));
        if (!await db.JobPreferences.AnyAsync(p => p.UserId == user.Id, ct))
            db.JobPreferences.Add(DemoCandidate.Preferences(user.Id));

        // Databases seeded before onboarding existed: the demo user is already set up.
        await db.CandidateProfiles.Where(p => p.UserId == user.Id && p.OnboardingCompletedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.OnboardingCompletedAt, clock.GetUtcNow()), ct);

        await db.SaveChangesAsync(ct);
        return user;
    }
}
