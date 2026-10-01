using System.ComponentModel.DataAnnotations;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Api.Contracts;

public sealed record PreferencesDto(
    decimal? MinSalary,
    decimal? MaxSalary,
    IReadOnlyList<string> PreferredRoles,
    IReadOnlyList<string> ExcludedRoles,
    IReadOnlyList<string> ExcludedKeywords,
    IReadOnlyList<string> PreferredIndustries,
    IReadOnlyList<WorkModality> PreferredModalities,
    IReadOnlyList<EmploymentType> EmploymentTypes,
    IReadOnlyList<string> PreferredDistricts,
    IReadOnlyList<string> ExcludedDistricts,
    string? HomeDistrict,
    int? MaxCommuteMinutes,
    bool WeekdaysOnly,
    EducationLevel? MaxRequiredEducation,
    NotificationFrequency NotificationFrequency,
    bool PushEnabled,
    DateTimeOffset UpdatedAt);

public sealed class UpdatePreferencesRequest : IValidatableObject
{
    [Range(0, 1_000_000)]
    public decimal? MinSalary { get; init; }

    [Range(0, 1_000_000)]
    public decimal? MaxSalary { get; init; }

    public List<string> PreferredRoles { get; init; } = [];
    public List<string> ExcludedRoles { get; init; } = [];
    public List<string> ExcludedKeywords { get; init; } = [];
    public List<string> PreferredIndustries { get; init; } = [];
    public List<WorkModality> PreferredModalities { get; init; } = [];
    public List<EmploymentType> EmploymentTypes { get; init; } = [];
    public List<string> PreferredDistricts { get; init; } = [];
    public List<string> ExcludedDistricts { get; init; } = [];

    [StringLength(80)]
    public string? HomeDistrict { get; init; }

    [Range(5, 240)]
    public int? MaxCommuteMinutes { get; init; }

    public bool WeekdaysOnly { get; init; }
    public EducationLevel? MaxRequiredEducation { get; init; }
    public NotificationFrequency NotificationFrequency { get; init; } = NotificationFrequency.Every6Hours;
    public bool PushEnabled { get; init; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinSalary is { } min && MaxSalary is { } max && max < min)
            yield return new ValidationResult("El sueldo máximo no puede ser menor que el mínimo.", [nameof(MaxSalary)]);

        var lists = new (string Name, List<string> Items)[]
        {
            (nameof(PreferredRoles), PreferredRoles), (nameof(ExcludedRoles), ExcludedRoles),
            (nameof(ExcludedKeywords), ExcludedKeywords), (nameof(PreferredIndustries), PreferredIndustries),
            (nameof(PreferredDistricts), PreferredDistricts), (nameof(ExcludedDistricts), ExcludedDistricts)
        };

        foreach (var (name, items) in lists)
            if (items.Count > 40 || items.Any(i => i.Length > 80))
                yield return new ValidationResult($"{name}: máximo 40 elementos de 80 caracteres.", [name]);
    }
}
