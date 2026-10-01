using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Resumes;

/// <summary>
/// What the deterministic parser understood from a CV. It is a *proposal*: the app shows it to the user for
/// review and only the edited version is saved to the profile (we never assume the extraction is right).
/// </summary>
public sealed class ParsedResume
{
    public string? FullNameGuess { get; set; }
    public string? Headline { get; set; }
    public int ExperienceMonths { get; set; }
    public EducationLevel? EducationLevel { get; set; }
    public EducationStatus? EducationStatus { get; set; }
    public List<ProfileSkill> Skills { get; set; } = [];
    public List<LanguageSkill> Languages { get; set; } = [];
    public List<WorkExperience> Experience { get; set; } = [];
    public List<EducationEntry> Education { get; set; } = [];
    public List<string> Certifications { get; set; } = [];
    public List<string> SuggestedRoles { get; set; } = [];
    /// <summary>Plain-Spanish notes about what could not be detected, shown during review.</summary>
    public List<string> Warnings { get; set; } = [];
}
