using ChambaIA.Api.Contracts;
using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Geo;
using ChambaIA.Domain.Text;

namespace ChambaIA.Api.Infrastructure;

/// <summary>Query-string values after parsing: enum filters are case-insensitive ("onSite", "OnSite").</summary>
public sealed record ParsedFilter(
    JobFilterQuery Raw,
    WorkModality? Modality,
    EmploymentType? EmploymentType,
    MatchTab Tab,
    MatchCategory? Category,
    FeedSort? Sort,
    int Page,
    int PageSize);

public static class JobQueryExtensions
{
    public const int MaxPageSize = 50;

    public static bool TryParseEnum<T>(string? raw, out T? value) where T : struct, Enum
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (Enum.TryParse<T>(raw.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            value = parsed;
            return true;
        }
        return false;
    }

    /// <summary>Parses the enum-valued query parameters; on failure returns a 400 ValidationProblem.</summary>
    public static bool TryParse(this JobFilterQuery f, out ParsedFilter parsed, out IResult? error)
    {
        var errors = new Dictionary<string, string[]>();
        if (!TryParseEnum<WorkModality>(f.Modality, out var modality)) errors["modality"] = ["Valor inválido."];
        if (!TryParseEnum<EmploymentType>(f.EmploymentType, out var type)) errors["employmentType"] = ["Valor inválido."];

        MatchTab? tab = null;
        MatchCategory? category = null;
        if (f is MatchFeedQuery m)
        {
            if (!TryParseEnum(m.Tab, out tab)) errors["tab"] = ["Valor inválido (forYou, new, saved)."];
            if (!TryParseEnum(m.Category, out category)) errors["category"] = ["Valor inválido."];
        }

        if (!TryParseEnum<FeedSort>(f.Sort, out var sort)) errors["sort"] = ["Valor inválido (relevance, recent, salary)."];

        parsed = new ParsedFilter(f, modality, type, tab ?? MatchTab.ForYou, category, sort,
            Math.Max(1, f.Page ?? 1), Math.Clamp(f.PageSize ?? 20, 1, MaxPageSize));
        error = errors.Count > 0 ? Results.ValidationProblem(errors) : null;
        return errors.Count == 0;
    }

    /// <summary>Active, non-expired offers narrowed by the user's filters. Shared by /jobs and /matches.</summary>
    public static IQueryable<JobOffer> ApplyFilters(this IQueryable<JobOffer> jobs, ParsedFilter p, DateTimeOffset now)
    {
        var f = p.Raw;
        jobs = jobs.Where(j => j.IsActive && (j.ExpiresAt == null || j.ExpiresAt > now));

        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var q = TextNormalizer.Normalize(f.Q);
            if (q.Length > 0)
                jobs = jobs.Where(j => j.NormalizedTitle.Contains(q) || j.NormalizedCompany.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(f.District))
        {
            var district = LimaDistricts.Canonical(f.District);
            jobs = jobs.Where(j => j.District == district);
        }

        if (p.Modality is { } modality) jobs = jobs.Where(j => j.Modality == modality);
        if (p.EmploymentType is { } type) jobs = jobs.Where(j => j.EmploymentType == type);
        if (!string.IsNullOrWhiteSpace(f.Industry))
        {
            var industry = f.Industry.Trim();
            jobs = jobs.Where(j => j.Industry == industry);
        }

        if (f.MinSalary is { } min) jobs = jobs.Where(j => (j.SalaryMax ?? j.SalaryMin) >= min);
        if (f.PostedWithinDays is { } days and > 0)
        {
            var since = now.AddDays(-days);
            jobs = jobs.Where(j => j.PostedAt >= since);
        }

        return jobs;
    }
}
