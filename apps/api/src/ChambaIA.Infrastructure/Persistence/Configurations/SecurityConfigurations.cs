using ChambaIA.Domain.Entities;
using ChambaIA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChambaIA.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.HasKey(t => t.Id);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Property(t => t.TokenHash).HasMaxLength(64);
        b.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
        b.HasIndex(t => t.TokenHash).IsUnique();
        b.HasIndex(t => t.UserId);
    }
}

internal sealed class AiUsageConfiguration : IEntityTypeConfiguration<AiUsage>
{
    public void Configure(EntityTypeBuilder<AiUsage> b)
    {
        b.HasKey(u => u.Id);
        b.Property(u => u.Provider).HasMaxLength(50);
        b.Property(u => u.Model).HasMaxLength(100);
        b.Property(u => u.Operation).HasMaxLength(60);
        b.Property(u => u.EstimatedCost).HasPrecision(12, 6);
        b.HasIndex(u => new { u.UserId, u.CreatedAt });
        b.HasIndex(u => new { u.Provider, u.CreatedAt });
    }
}
