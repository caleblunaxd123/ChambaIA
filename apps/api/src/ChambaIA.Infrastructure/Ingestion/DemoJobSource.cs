using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Ingestion;
using ChambaIA.Infrastructure.Seeding;

namespace ChambaIA.Infrastructure.Ingestion;

/// <summary>
/// The fictional offers, served through the real pipeline (normaliser, dedup, matching) so the demo exercises exactly
/// what production sources will. Offers are re-dated on every fetch so "Nuevas" never goes stale.
/// </summary>
public sealed class DemoJobSource(TimeProvider clock) : IJobSource
{
    public const string SourceKey = "demo";

    public string Key => SourceKey;
    public string Name => "Ofertas de demostración";
    public JobSourceKind Kind => JobSourceKind.Demo;
    public string? BaseUrl => "https://example.com/empleos";

    public Task<IReadOnlyList<RawJob>> FetchJobsAsync(DateTimeOffset? since, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        IReadOnlyList<RawJob> jobs = DemoJobs.All.Select(d =>
        {
            var posted = now.AddHours(-d.HoursAgo);
            return new RawJob
            {
                ExternalId = d.Id,
                Title = d.Title,
                Company = d.Company,
                Description = d.Description,
                Url = $"https://example.com/empleos/{d.Id}",
                Location = d.District,
                SalaryMin = d.SalaryMin,
                SalaryMax = d.SalaryMax,
                Modality = d.Modality switch { WorkModality.Remote => "Remoto", WorkModality.Hybrid => "Híbrido", _ => "Presencial" },
                EmploymentType = d.Type switch
                {
                    EmploymentType.PartTime => "Medio tiempo",
                    EmploymentType.Internship => "Practicante",
                    EmploymentType.Temporary => "Temporal",
                    EmploymentType.Freelance => "Freelance",
                    _ => "Tiempo completo"
                },
                Schedule = d.Schedule,
                Industry = d.Industry,
                PostedAt = posted,
                ExpiresAt = posted.AddDays(30),
                ExperienceMonths = d.ExperienceMonths,
                Education = d.Education,
                EducationCompleted = d.EducationCompleted,
                WeekdaysOnly = d.WeekdaysOnly,
                SkillsRequired = d.Required.Select(r => new RawSkill(r.Skill, r.Level)).ToList(),
                SkillsPreferred = d.Preferred
            };
        }).ToList();

        return Task.FromResult(jobs);
    }
}
