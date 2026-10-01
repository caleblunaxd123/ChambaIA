using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Skills;
using ChambaIA.Domain.Text;
using ChambaIA.Infrastructure.Identity;
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
    IOptions<SeedOptions> options,
    TimeProvider clock,
    ILogger<DemoDataSeeder> logger)
{
    private const string SourceKey = "demo";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!opts.Enabled) return;
        if (string.IsNullOrWhiteSpace(opts.DemoPassword))
        {
            logger.LogWarning("Seed:Enabled is true but Seed:DemoPassword is empty; skipping demo data.");
            return;
        }

        var source = await EnsureSourceAsync(ct);
        await EnsureJobsAsync(source, ct);
        var user = await EnsureDemoUserAsync(opts, ct);
        await matcher.RecomputeForUserAsync(user.Id, ct);

        logger.LogInformation("Demo data ready ({Email}, {Jobs} offers).", opts.DemoEmail, DemoJobs.All.Count);
    }

    private async Task<JobSource> EnsureSourceAsync(CancellationToken ct)
    {
        var source = await db.JobSources.SingleOrDefaultAsync(s => s.Key == SourceKey, ct);
        if (source is not null) return source;

        source = new JobSource
        {
            Key = SourceKey,
            Name = "Ofertas de demostración",
            Kind = JobSourceKind.Demo,
            Notes = "Datos ficticios para validar la experiencia antes de conectar fuentes reales."
        };
        db.JobSources.Add(source);
        await db.SaveChangesAsync(ct);
        return source;
    }

    private async Task EnsureJobsAsync(JobSource source, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var existing = await db.JobOffers
            .Where(j => j.SourceId == source.Id)
            .ToDictionaryAsync(j => j.ExternalId, ct);

        foreach (var demo in DemoJobs.All)
        {
            if (!existing.TryGetValue(demo.Id, out var job))
            {
                job = new JobOffer { SourceId = source.Id, ExternalId = demo.Id };
                db.JobOffers.Add(job);
            }

            Map(demo, job, source, now);
        }

        source.LastFetchedAt = now;
        await db.SaveChangesAsync(ct);
    }

    private static void Map(DemoJob demo, JobOffer job, JobSource source, DateTimeOffset now)
    {
        var posted = now.AddHours(-demo.HoursAgo);

        job.SourceName = source.Name;
        job.OriginalUrl = $"https://example.com/empleos/{demo.Id}";
        job.Title = demo.Title;
        job.NormalizedTitle = TextNormalizer.Normalize(demo.Title);
        job.Company = demo.Company;
        job.NormalizedCompany = TextNormalizer.Normalize(demo.Company);
        job.Description = demo.Description;
        job.NormalizedDescription = TextNormalizer.Normalize(demo.Description);
        job.SalaryMin = demo.SalaryMin;
        job.SalaryMax = demo.SalaryMax;
        job.District = demo.District;
        job.Modality = demo.Modality;
        job.EmploymentType = demo.Type;
        job.Industry = demo.Industry;
        job.Schedule = demo.Schedule;
        job.WeekdaysOnly = demo.WeekdaysOnly;
        job.ExperienceRequiredMinMonths = demo.ExperienceMonths;
        job.EducationRequired = demo.Education;
        job.EducationRequiredCompleted = demo.EducationCompleted;
        job.SkillsRequired = demo.Required.Select(r => Requirement(r.Skill, r.Level)).ToList();
        job.SkillsPreferred = demo.Preferred.Select(k => Requirement(k, null)).ToList();
        job.PostedAt = posted;
        job.ExpiresAt = posted.AddDays(30);
        job.LastSeenAt = now;
        job.IsActive = true;
        job.ContentHash = TextNormalizer.ContentHash(demo.Title, demo.Company, demo.District, demo.Description);
    }

    private static SkillRequirement Requirement(string key, SkillLevel? level) => new()
    {
        Key = key,
        Name = SkillCatalog.FindByKey(key)?.Name ?? key,
        MinLevel = level
    };

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
