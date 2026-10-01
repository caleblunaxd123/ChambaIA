using ChambaIA.Domain.Entities;
using ChambaIA.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<JobPreferences> JobPreferences => Set<JobPreferences>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<JobSource> JobSources => Set<JobSource>();
    public DbSet<JobOffer> JobOffers => Set<JobOffer>();
    public DbSet<CandidateJobMatch> Matches => Set<CandidateJobMatch>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AiUsage> AiUsages => Set<AiUsage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as readable strings: safer across migrations than ordinals and easy to query by hand.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("vector");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
