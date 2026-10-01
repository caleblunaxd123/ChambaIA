using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Entities;

/// <summary>What the candidate wants. Drives the hard filters of the matching engine.</summary>
public class JobPreferences
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }

    public List<string> PreferredRoles { get; set; } = [];
    public List<string> ExcludedRoles { get; set; } = [];
    /// <summary>Words that, if present in the title or description, hide the offer (e.g. "call center").</summary>
    public List<string> ExcludedKeywords { get; set; } = [];
    public List<string> PreferredIndustries { get; set; } = [];

    public List<WorkModality> PreferredModalities { get; set; } = [];
    public List<EmploymentType> EmploymentTypes { get; set; } = [];

    public List<string> PreferredDistricts { get; set; } = [];
    public List<string> ExcludedDistricts { get; set; } = [];
    public string? HomeDistrict { get; set; }
    public int? MaxCommuteMinutes { get; set; }

    public bool WeekdaysOnly { get; set; }

    /// <summary>Highest education level the candidate accepts a job to demand as completed (null = no limit).</summary>
    public EducationLevel? MaxRequiredEducation { get; set; }

    public NotificationFrequency NotificationFrequency { get; set; } = NotificationFrequency.Every6Hours;
    public bool PushEnabled { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
