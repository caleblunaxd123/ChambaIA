using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Entities;

public enum ApplicationEventKind { StatusChanged, InterviewScheduled, InterviewCleared }

/// <summary>One line of the history of a tracker card: what changed and when. Deleted together with the card.</summary>
public class ApplicationEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ApplicationId { get; set; }
    public ApplicationEventKind Kind { get; set; }
    /// <summary>Null when the card was just created.</summary>
    public ApplicationStatus? FromStatus { get; set; }
    public ApplicationStatus? ToStatus { get; set; }
    /// <summary>For <see cref="ApplicationEventKind.InterviewScheduled"/>: the date that was set.</summary>
    public DateTimeOffset? InterviewDate { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
