using ChambaIA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChambaIA.Infrastructure.Persistence.Configurations;

internal sealed class JobSourceConfiguration : IEntityTypeConfiguration<JobSource>
{
    public void Configure(EntityTypeBuilder<JobSource> b)
    {
        b.HasKey(s => s.Id);
        b.HasIndex(s => s.Key).IsUnique();
        b.Property(s => s.Key).HasMaxLength(50);
        b.Property(s => s.Name).HasMaxLength(100);
        b.Property(s => s.BaseUrl).HasMaxLength(300);
        b.Property(s => s.LastError).HasMaxLength(500);
    }
}

internal sealed class JobOfferConfiguration : IEntityTypeConfiguration<JobOffer>
{
    public void Configure(EntityTypeBuilder<JobOffer> b)
    {
        b.HasKey(j => j.Id);
        b.HasOne(j => j.Source).WithMany().HasForeignKey(j => j.SourceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<JobOffer>().WithMany().HasForeignKey(j => j.DuplicateOfId).OnDelete(DeleteBehavior.SetNull);

        b.Property(j => j.ExternalId).HasMaxLength(200);
        b.Property(j => j.SourceName).HasMaxLength(100);
        b.Property(j => j.OriginalUrl).HasMaxLength(1000);
        b.Property(j => j.Title).HasMaxLength(250);
        b.Property(j => j.NormalizedTitle).HasMaxLength(250);
        b.Property(j => j.Company).HasMaxLength(200);
        b.Property(j => j.NormalizedCompany).HasMaxLength(200);
        b.Property(j => j.District).HasMaxLength(80);
        b.Property(j => j.City).HasMaxLength(80);
        b.Property(j => j.Country).HasMaxLength(2);
        b.Property(j => j.SalaryCurrency).HasMaxLength(3);
        b.Property(j => j.Industry).HasMaxLength(100);
        b.Property(j => j.Schedule).HasMaxLength(200);
        b.Property(j => j.ContentHash).HasMaxLength(64);
        b.Property(j => j.SalaryMin).HasPrecision(12, 2);
        b.Property(j => j.SalaryMax).HasPrecision(12, 2);
        b.Property(j => j.Embedding).HasColumnType("vector(1024)");
        b.Property(j => j.EmbeddingHash).HasMaxLength(64);

        b.OwnsMany(j => j.SkillsRequired, o => o.ToJson());
        b.OwnsMany(j => j.SkillsPreferred, o => o.ToJson());

        b.HasIndex(j => new { j.SourceId, j.ExternalId }).IsUnique();
        b.HasIndex(j => new { j.IsActive, j.PostedAt });
        b.HasIndex(j => j.District);
        b.HasIndex(j => j.ContentHash);
        b.HasIndex(j => new { j.NormalizedCompany, j.NormalizedTitle });
        b.HasIndex(j => j.DuplicateOfId);
        // Approximate nearest-neighbour search by cosine distance (bge-m3 embeddings are compared by angle).
        b.HasIndex(j => j.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops").HasStorageParameter("m", 16).HasStorageParameter("ef_construction", 64);
    }
}

internal sealed class CandidateJobMatchConfiguration : IEntityTypeConfiguration<CandidateJobMatch>
{
    public void Configure(EntityTypeBuilder<CandidateJobMatch> b)
    {
        b.HasKey(m => m.Id);
        b.HasOne(m => m.Candidate).WithMany().HasForeignKey(m => m.CandidateId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(m => m.Job).WithMany().HasForeignKey(m => m.JobId).OnDelete(DeleteBehavior.Cascade);

        b.Property(m => m.MatchedSkills).HasColumnType("text[]");
        b.Property(m => m.MissingSkills).HasColumnType("text[]");
        b.OwnsMany(m => m.Reasons, o => o.ToJson());
        b.OwnsMany(m => m.Warnings, o => o.ToJson());

        b.HasIndex(m => new { m.CandidateId, m.JobId }).IsUnique();
        b.HasIndex(m => new { m.CandidateId, m.Status });
        b.HasIndex(m => new { m.CandidateId, m.OverallScore });
        b.HasIndex(m => m.JobId);
    }
}

internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> b)
    {
        b.HasKey(a => a.Id);
        b.HasOne(a => a.Job).WithMany().HasForeignKey(a => a.JobId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Identity.ApplicationUser>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Property(a => a.Notes).HasMaxLength(2000);
        b.Property(a => a.SalaryOffered).HasPrecision(12, 2);

        b.HasIndex(a => new { a.UserId, a.JobId }).IsUnique();
        b.HasIndex(a => new { a.UserId, a.Status });
    }
}
