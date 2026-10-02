using System.ComponentModel.DataAnnotations;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Api.Contracts;

public sealed record ApplicationDto(
    Guid Id,
    Guid JobId,
    ApplicationStatus Status,
    DateTimeOffset? AppliedAt,
    string? Notes,
    DateTimeOffset? InterviewDate,
    decimal? SalaryOffered,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    JobSummaryDto? Job);

public sealed class CreateApplicationRequest
{
    [Required]
    public Guid JobId { get; init; }

    public ApplicationStatus Status { get; init; } = ApplicationStatus.Interested;

    [StringLength(2000)]
    public string? Notes { get; init; }
}

/// <summary>Partial update: only the fields that are present are changed.</summary>
public sealed class PatchApplicationRequest
{
    public ApplicationStatus? Status { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

    public DateTimeOffset? InterviewDate { get; init; }

    /// <summary>JSON null cannot tell "leave as is" from "remove", so removing the interview date is explicit.</summary>
    public bool? ClearInterviewDate { get; init; }

    [Range(0, 1_000_000)]
    public decimal? SalaryOffered { get; init; }
}
