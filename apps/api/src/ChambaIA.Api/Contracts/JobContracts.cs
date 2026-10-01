using ChambaIA.Domain.Enums;

namespace ChambaIA.Api.Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total, bool HasMore);

public sealed record JobSummaryDto(
    Guid Id,
    string Title,
    string Company,
    string? District,
    string City,
    WorkModality Modality,
    EmploymentType EmploymentType,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string SalaryCurrency,
    string? SalaryLabel,
    string? Industry,
    string SourceName,
    DateTimeOffset? PostedAt);

public sealed record SkillRequirementDto(string Key, string Name, SkillLevel? MinLevel);

public sealed record JobDetailDto(
    JobSummaryDto Summary,
    string Description,
    string? Schedule,
    bool? WeekdaysOnly,
    int? ExperienceRequiredMinMonths,
    EducationLevel? EducationRequired,
    bool EducationRequiredCompleted,
    IReadOnlyList<SkillRequirementDto> SkillsRequired,
    IReadOnlyList<SkillRequirementDto> SkillsPreferred,
    string OriginalUrl,
    DateTimeOffset? ExpiresAt);

public sealed record MatchNoteDto(string Code, string Title, string? Detail);

/// <summary>Compact match info for cards. Deliberately has no percentage: categories are the UX contract.</summary>
public sealed record MatchSummaryDto(
    MatchCategory Category,
    string CategoryLabel,
    MatchStatus Status,
    IReadOnlyList<string> TopReasons,
    IReadOnlyList<string> TopWarnings);

public sealed record MatchDetailDto(
    MatchCategory Category,
    string CategoryLabel,
    string Recommendation,
    MatchStatus Status,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<MatchNoteDto> Reasons,
    IReadOnlyList<MatchNoteDto> Warnings);

public sealed record FeedItemDto(JobSummaryDto Job, MatchSummaryDto? Match);

public sealed record JobDetailResponse(JobDetailDto Job, MatchDetailDto? Match, ApplicationDto? Application);

public sealed record MatchDetailResponse(JobSummaryDto Job, MatchDetailDto Match);

/// <summary>
/// "New*" fields describe offers the user has not opened yet ("Encontré 7 nuevas: 3 encajan muy bien…").
/// The rest summarise everything currently in the feed (excluding dismissed offers).
/// Strong = excellent + very compatible, Possible = compatible, Review = review.
/// </summary>
public sealed record MatchOverviewDto(
    int NewTotal,
    int NewStrong,
    int NewPossible,
    int NewNotRecommended,
    int Strong,
    int Possible,
    int Review,
    int NotRecommended,
    DateTimeOffset? LastUpdatedAt);

public enum MatchTab { ForYou, New, Saved }

public class JobFilterQuery
{
    public string? Q { get; init; }
    public string? District { get; init; }
    public string? Modality { get; init; }
    public string? EmploymentType { get; init; }
    public string? Industry { get; init; }
    public decimal? MinSalary { get; init; }
    public int? PostedWithinDays { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

public sealed class MatchFeedQuery : JobFilterQuery
{
    public string? Tab { get; init; }
    public string? Category { get; init; }
}
