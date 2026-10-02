using ChambaIA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChambaIA.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationEventConfiguration : IEntityTypeConfiguration<ApplicationEvent>
{
    public void Configure(EntityTypeBuilder<ApplicationEvent> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.ApplicationId, e.At });
        // The history lives and dies with its card: removing a card (or the account) removes its history.
        b.HasOne<JobApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Cascade);
    }
}
