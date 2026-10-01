using ChambaIA.Domain.Enums;
using Pgvector;

namespace ChambaIA.Domain.Entities;

/// <summary>Who the candidate is: structured, user-editable facts extracted from the CV.</summary>
public class CandidateProfile
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    public string? Headline { get; set; }
    public int ExperienceMonths { get; set; }

    public EducationLevel? EducationLevel { get; set; }
    public EducationStatus? EducationStatus { get; set; }

    public List<ProfileSkill> Skills { get; set; } = [];
    public List<LanguageSkill> Languages { get; set; } = [];
    public List<WorkExperience> Experience { get; set; } = [];
    public List<EducationEntry> Education { get; set; } = [];
    public List<string> Certifications { get; set; } = [];

    /// <summary>Set when the user finishes (or explicitly completes) onboarding. Null = the app guides them through it.</summary>
    public DateTimeOffset? OnboardingCompletedAt { get; set; }

    /// <summary>Filled by the embedding stage (phase 4). Dimension matches the configured model (bge-m3 = 1024).</summary>
    public Vector? ProfileEmbedding { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ProfileSkill
{
    /// <summary>Canonical key from <see cref="Skills.SkillCatalog"/> (e.g. "excel").</summary>
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public SkillLevel Level { get; set; } = SkillLevel.Basic;
}

public class LanguageSkill
{
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
}

public class WorkExperience
{
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Description { get; set; }
}

public class EducationEntry
{
    public string Institution { get; set; } = "";
    public string Degree { get; set; } = "";
    public EducationLevel Level { get; set; }
    public EducationStatus Status { get; set; }
}
