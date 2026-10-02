using ChambaIA.Domain.Entities;
using ChambaIA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChambaIA.Infrastructure.Persistence.Configurations;

internal sealed class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> b)
    {
        b.HasKey(p => p.Id);
        b.HasIndex(p => p.UserId).IsUnique();
        b.HasOne<ApplicationUser>().WithOne().HasForeignKey<CandidateProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Property(p => p.Headline).HasMaxLength(200);
        b.Property(p => p.Certifications).HasColumnType("text[]");
        b.Property(p => p.ProfileEmbedding).HasColumnType("vector(1024)");
        b.Property(p => p.EmbeddingHash).HasMaxLength(64);

        b.OwnsMany(p => p.Skills, o => o.ToJson());
        b.OwnsMany(p => p.Languages, o => o.ToJson());
        b.OwnsMany(p => p.Experience, o => o.ToJson());
        b.OwnsMany(p => p.Education, o => o.ToJson());
    }
}

internal sealed class JobPreferencesConfiguration : IEntityTypeConfiguration<JobPreferences>
{
    public void Configure(EntityTypeBuilder<JobPreferences> b)
    {
        b.HasKey(p => p.Id);
        b.HasIndex(p => p.UserId).IsUnique();
        b.HasOne<ApplicationUser>().WithOne().HasForeignKey<JobPreferences>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Property(p => p.MinSalary).HasPrecision(12, 2);
        b.Property(p => p.MaxSalary).HasPrecision(12, 2);
        b.Property(p => p.HomeDistrict).HasMaxLength(80);

        b.Property(p => p.PreferredRoles).HasColumnType("text[]");
        b.Property(p => p.ExcludedRoles).HasColumnType("text[]");
        b.Property(p => p.ExcludedKeywords).HasColumnType("text[]");
        b.Property(p => p.PreferredIndustries).HasColumnType("text[]");
        b.Property(p => p.PreferredDistricts).HasColumnType("text[]");
        b.Property(p => p.ExcludedDistricts).HasColumnType("text[]");
        b.Property(p => p.PreferredModalities).HasColumnType("text[]");
        b.Property(p => p.EmploymentTypes).HasColumnType("text[]");
    }
}

internal sealed class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> b)
    {
        b.HasKey(r => r.Id);
        b.HasIndex(r => r.UserId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Property(r => r.FileUrl).HasMaxLength(300);
        b.Property(r => r.OriginalFilename).HasMaxLength(260);
        b.Property(r => r.MimeType).HasMaxLength(100);
        b.Property(r => r.StructuredData).HasColumnType("jsonb");
    }
}
