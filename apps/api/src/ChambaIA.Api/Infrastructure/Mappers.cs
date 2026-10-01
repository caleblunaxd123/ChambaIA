using ChambaIA.Api.Contracts;
using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Matching;

namespace ChambaIA.Api.Infrastructure;

/// <summary>Entity → DTO mapping. Kept in one place so every endpoint renders jobs and matches identically.</summary>
public static class Mappers
{
    public static JobSummaryDto ToSummary(this JobOffer j) => new(
        j.Id, j.Title, j.Company, j.District, j.City, j.Modality, j.EmploymentType,
        j.SalaryMin, j.SalaryMax, j.SalaryCurrency,
        MatchFormatting.SalaryRange(j.SalaryMin, j.SalaryMax) is { Length: > 0 } label ? label : null,
        j.Industry, j.SourceName, j.PostedAt);

    public static JobDetailDto ToDetail(this JobOffer j) => new(
        j.ToSummary(), j.Description, j.Schedule, j.WeekdaysOnly, j.ExperienceRequiredMinMonths,
        j.EducationRequired, j.EducationRequiredCompleted,
        j.SkillsRequired.Select(s => new SkillRequirementDto(s.Key, s.Name, s.MinLevel)).ToList(),
        j.SkillsPreferred.Select(s => new SkillRequirementDto(s.Key, s.Name, s.MinLevel)).ToList(),
        j.OriginalUrl, j.ExpiresAt);

    public static MatchSummaryDto ToSummary(this CandidateJobMatch m) => new(
        m.Category, MatchFormatting.Category(m.Category), m.Status,
        m.Reasons.Take(3).Select(n => n.Title).ToList(),
        m.Warnings.Take(2).Select(n => n.Title).ToList());

    public static MatchDetailDto ToDetail(this CandidateJobMatch m) => new(
        m.Category, MatchFormatting.Category(m.Category), MatchFormatting.Recommendation(m.Category), m.Status,
        m.MatchedSkills, m.MissingSkills,
        m.Reasons.Select(n => new MatchNoteDto(n.Code, n.Title, n.Detail)).ToList(),
        m.Warnings.Select(n => new MatchNoteDto(n.Code, n.Title, n.Detail)).ToList());

    public static ApplicationDto ToDto(this JobApplication a) => new(
        a.Id, a.JobId, a.Status, a.AppliedAt, a.Notes, a.InterviewDate, a.SalaryOffered,
        a.CreatedAt, a.UpdatedAt, a.Job?.ToSummary());
}
