using System.ComponentModel.DataAnnotations;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Api.Contracts;

public sealed record SkillDto(string Key, string Name, SkillLevel Level);

public sealed record LanguageDto(string Name, string Level);

public sealed record ExperienceDto(string Title, string Company, DateOnly? StartDate, DateOnly? EndDate, string? Description);

public sealed record EducationDto(string Institution, string Degree, EducationLevel Level, EducationStatus Status);

public sealed record ProfileDto(
    string FullName,
    string Email,
    string? Headline,
    int ExperienceMonths,
    EducationLevel? EducationLevel,
    EducationStatus? EducationStatus,
    IReadOnlyList<SkillDto> Skills,
    IReadOnlyList<LanguageDto> Languages,
    IReadOnlyList<ExperienceDto> Experience,
    IReadOnlyList<EducationDto> Education,
    IReadOnlyList<string> Certifications,
    bool OnboardingCompleted,
    DateTimeOffset UpdatedAt);

public sealed class UpdateProfileRequest : IValidatableObject
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; init; } = "";

    [StringLength(200)]
    public string? Headline { get; init; }

    [Range(0, 720)]
    public int ExperienceMonths { get; init; }

    public EducationLevel? EducationLevel { get; init; }
    public EducationStatus? EducationStatus { get; init; }

    public List<SkillDto> Skills { get; init; } = [];
    public List<LanguageDto> Languages { get; init; } = [];
    public List<ExperienceDto> Experience { get; init; } = [];
    public List<EducationDto> Education { get; init; } = [];
    public List<string> Certifications { get; init; } = [];

    /// <summary>True when the user finishes onboarding with this save.</summary>
    public bool CompleteOnboarding { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Skills.Count > 60) yield return new ValidationResult("Máximo 60 habilidades.", [nameof(Skills)]);
        if (Skills.Any(s => string.IsNullOrWhiteSpace(s.Name) || s.Name.Length > 80))
            yield return new ValidationResult("Cada habilidad necesita un nombre de hasta 80 caracteres.", [nameof(Skills)]);
        if (Experience.Count > 30) yield return new ValidationResult("Máximo 30 experiencias.", [nameof(Experience)]);
        if (Education.Count > 20) yield return new ValidationResult("Máximo 20 estudios.", [nameof(Education)]);
        if (Certifications.Count > 30 || Certifications.Any(c => c.Length > 150))
            yield return new ValidationResult("Certificaciones inválidas.", [nameof(Certifications)]);
    }
}
