using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Ingestion;

/// <summary>
/// An offer exactly as a connector received it. Only the identity, title, company, description and URL are required:
/// every structured field is optional and, when missing, <see cref="JobNormalizer"/> infers it from the text.
/// </summary>
public sealed record RawJob
{
    /// <summary>Stable id inside its source; together with the source it makes ingestion idempotent.</summary>
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public required string Company { get; init; }
    public required string Description { get; init; }
    public required string Url { get; init; }

    /// <summary>Free text such as "Los Olivos, Lima" or "Remoto".</summary>
    public string? Location { get; init; }
    public decimal? SalaryMin { get; init; }
    public decimal? SalaryMax { get; init; }
    /// <summary>Free text such as "S/ 1,800 - 2,200" when the source does not split the numbers.</summary>
    public string? SalaryText { get; init; }
    public string? Modality { get; init; }
    public string? EmploymentType { get; init; }
    public string? Schedule { get; init; }
    public string? Industry { get; init; }
    public DateTimeOffset? PostedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }

    // Structured requirements, when the source provides them (otherwise inferred from the description).
    public int? ExperienceMonths { get; init; }
    public EducationLevel? Education { get; init; }
    public bool? EducationCompleted { get; init; }
    public bool? WeekdaysOnly { get; init; }
    public IReadOnlyList<RawSkill>? SkillsRequired { get; init; }
    public IReadOnlyList<string>? SkillsPreferred { get; init; }
}

/// <summary>A required skill: a catalogue key or free text, plus an optional minimum level.</summary>
public sealed record RawSkill(string Skill, SkillLevel? MinLevel = null);
