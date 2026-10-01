using ChambaIA.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace ChambaIA.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string FullName { get; set; } = "";
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
