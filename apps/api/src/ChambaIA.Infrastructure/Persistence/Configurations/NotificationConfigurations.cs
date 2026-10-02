using ChambaIA.Domain.Entities;
using ChambaIA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChambaIA.Infrastructure.Persistence.Configurations;

internal sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> b)
    {
        b.HasKey(d => d.Id);
        b.Property(d => d.Token).HasMaxLength(200);
        b.Property(d => d.Platform).HasMaxLength(20);
        b.HasIndex(d => d.Token).IsUnique();
        b.HasIndex(d => d.UserId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> b)
    {
        b.HasKey(n => n.Id);
        b.ToTable("Notifications");
        b.Property(n => n.Title).HasMaxLength(200);
        b.Property(n => n.Body).HasMaxLength(500);
        b.HasIndex(n => new { n.UserId, n.CreatedAt });
        b.HasIndex(n => new { n.UserId, n.Kind, n.CreatedAt });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
