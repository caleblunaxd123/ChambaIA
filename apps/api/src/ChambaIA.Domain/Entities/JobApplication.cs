using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Entities;

/// <summary>Tracker card: the user's own pipeline for a job (Found, Interested, Applied, ...).</summary>
public class JobApplication
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid JobId { get; set; }
    public JobOffer? Job { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Found;
    public DateTimeOffset? AppliedAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? InterviewDate { get; set; }
    public decimal? SalaryOffered { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
