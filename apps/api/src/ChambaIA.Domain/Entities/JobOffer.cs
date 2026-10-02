using ChambaIA.Domain.Enums;
using Pgvector;

namespace ChambaIA.Domain.Entities;

public class JobOffer
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string ExternalId { get; set; } = "";
    public Guid SourceId { get; set; }
    public JobSource? Source { get; set; }
    public string SourceName { get; set; } = "";
    public string OriginalUrl { get; set; } = "";

    public string Title { get; set; } = "";
    public string NormalizedTitle { get; set; } = "";
    public string Company { get; set; } = "";
    public string NormalizedCompany { get; set; } = "";
    public string Description { get; set; } = "";
    public string NormalizedDescription { get; set; } = "";

    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string SalaryCurrency { get; set; } = "PEN";

    public string? District { get; set; }
    public string City { get; set; } = "Lima";
    public string Country { get; set; } = "PE";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public WorkModality Modality { get; set; } = WorkModality.OnSite;
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    public string? Industry { get; set; }
    public string? Schedule { get; set; }
    /// <summary>Null = the offer does not say.</summary>
    public bool? WeekdaysOnly { get; set; }

    public int? ExperienceRequiredMinMonths { get; set; }
    public int? ExperienceRequiredMaxMonths { get; set; }
    public EducationLevel? EducationRequired { get; set; }
    /// <summary>True when the offer demands the studies to be finished (egresado/titulado).</summary>
    public bool EducationRequiredCompleted { get; set; }

    public List<SkillRequirement> SkillsRequired { get; set; } = [];
    public List<SkillRequirement> SkillsPreferred { get; set; } = [];

    public DateTimeOffset? PostedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Set when ingestion recognised this offer as the same job published elsewhere. Duplicates are stored (so the
    /// pipeline stays idempotent and we know where else it appeared) but never shown or matched.
    /// </summary>
    public Guid? DuplicateOfId { get; set; }

    public string ContentHash { get; set; } = "";
    public Vector? Embedding { get; set; }
    /// <summary>Hash of the text (and model) the embedding was made from. A mismatch means it is stale and must be redone.</summary>
    public string? EmbeddingHash { get; set; }
}

public class SkillRequirement
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public SkillLevel? MinLevel { get; set; }
}
