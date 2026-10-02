using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Geo;
using ChambaIA.Domain.Skills;
using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Ingestion;

/// <summary>Everything the pipeline stores about an offer, already cleaned and inferred. Applied onto a <see cref="JobOffer"/>.</summary>
public sealed record NormalizedJob(
    string ExternalId,
    string Url,
    string Title,
    string Company,
    string Description,
    string? District,
    string City,
    WorkModality Modality,
    EmploymentType EmploymentType,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? Schedule,
    string? Industry,
    bool? WeekdaysOnly,
    int? ExperienceMonths,
    EducationLevel? Education,
    bool EducationCompleted,
    IReadOnlyList<SkillRequirement> SkillsRequired,
    IReadOnlyList<SkillRequirement> SkillsPreferred,
    DateTimeOffset PostedAt,
    DateTimeOffset ExpiresAt,
    string ContentHash)
{
    public void ApplyTo(JobOffer job, string sourceName, DateTimeOffset now)
    {
        job.ExternalId = ExternalId;
        job.SourceName = sourceName;
        job.OriginalUrl = Url;
        job.Title = Title;
        job.NormalizedTitle = TextNormalizer.Normalize(Title);
        job.Company = Company;
        job.NormalizedCompany = TextNormalizer.Normalize(Company);
        job.Description = Description;
        job.NormalizedDescription = TextNormalizer.Normalize(Description);
        job.District = District;
        job.City = City;
        var district = LimaDistricts.Find(District);
        job.Latitude = district?.Latitude;
        job.Longitude = district?.Longitude;
        job.Modality = Modality;
        job.EmploymentType = EmploymentType;
        job.SalaryMin = SalaryMin;
        job.SalaryMax = SalaryMax;
        job.SalaryCurrency = "PEN";
        job.Schedule = Schedule;
        job.Industry = Industry;
        job.WeekdaysOnly = WeekdaysOnly;
        job.ExperienceRequiredMinMonths = ExperienceMonths;
        job.EducationRequired = Education;
        job.EducationRequiredCompleted = EducationCompleted;
        job.SkillsRequired = SkillsRequired.Select(Copy).ToList();
        job.SkillsPreferred = SkillsPreferred.Select(Copy).ToList();
        job.PostedAt = PostedAt;
        job.ExpiresAt = ExpiresAt;
        job.LastSeenAt = now;
        job.ContentHash = ContentHash;
    }

    private static SkillRequirement Copy(SkillRequirement s) => new() { Key = s.Key, Name = s.Name, MinLevel = s.MinLevel };
}

public sealed record NormalizeResult(NormalizedJob? Job, string? Error)
{
    public bool IsValid => Job is not null;
}

/// <summary>
/// Turns a <see cref="RawJob"/> into a <see cref="NormalizedJob"/>. Pure and deterministic (no AI, no I/O): structured
/// fields from the source win; whatever is missing is inferred from the title, location and description with
/// conservative Spanish rules. When a rule is unsure it leaves the field empty rather than guessing.
/// </summary>
public static partial class JobNormalizer
{
    public const int MaxDescriptionLength = 8000;
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(30);

    public static NormalizeResult Normalize(RawJob raw, DateTimeOffset now)
    {
        var externalId = raw.ExternalId?.Trim() ?? "";
        var title = Clean(raw.Title, 250);
        var company = Clean(raw.Company, 200);
        var description = CleanDescription(raw.Description);

        if (externalId.Length is 0 or > 200) return Fail("Falta el identificador de la oferta o es demasiado largo.");
        if (title.Length == 0) return Fail("La oferta no tiene título.");
        if (company.Length == 0) return Fail("La oferta no indica la empresa.");
        if (!Uri.TryCreate(raw.Url?.Trim(), UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
            return Fail("La oferta no tiene un enlace válido a la página original.");

        var text = $"{title}. {raw.Location}. {raw.Modality}. {raw.EmploymentType}. {raw.Schedule}. {description}";
        var normalized = TextNormalizer.Normalize(text);

        var modality = InferModality($"{raw.Modality} {raw.Location} {title} {description}");
        var district = modality == WorkModality.Remote && raw.Location is null ? null : FindDistrict(raw.Location, title);
        var city = district is not null ? "Lima" : CityFrom(raw.Location);

        var (salaryMin, salaryMax) = raw.SalaryMin is not null || raw.SalaryMax is not null
            ? (raw.SalaryMin, raw.SalaryMax)
            : ParseSalary(raw.SalaryText, requireCurrency: false) is { } fromText ? fromText
            : ParseSalary(description, requireCurrency: true) ?? (null, null);
        if (salaryMin is not null && salaryMax is not null && salaryMin > salaryMax) (salaryMin, salaryMax) = (salaryMax, salaryMin);

        var (education, completed) = raw.Education is not null
            ? (raw.Education, raw.EducationCompleted ?? false)
            : InferEducation(normalized);

        var (required, preferred) = Skills(raw, title, description);

        // Sources publish local times ("-05:00" in Peru); storage (timestamptz) and comparisons are always in UTC.
        var posted = raw.PostedAt?.ToUniversalTime() is { } p && p <= now ? p : now.ToUniversalTime();
        var expires = raw.ExpiresAt?.ToUniversalTime() is { } e && e > posted ? e : posted + DefaultLifetime;
        var schedule = raw.Schedule is null ? null : Clean(raw.Schedule, 200);

        var job = new NormalizedJob(
            ExternalId: externalId,
            Url: url.ToString(),
            Title: title,
            Company: company,
            Description: description,
            District: district,
            City: city,
            Modality: modality,
            EmploymentType: InferEmploymentType($"{raw.EmploymentType} {title} {schedule}"),
            SalaryMin: Sane(salaryMin),
            SalaryMax: Sane(salaryMax),
            Schedule: schedule,
            Industry: raw.Industry is null ? null : Clean(raw.Industry, 100),
            WeekdaysOnly: raw.WeekdaysOnly ?? InferWeekdaysOnly(TextNormalizer.Normalize($"{schedule} {description}")),
            ExperienceMonths: raw.ExperienceMonths ?? InferExperienceMonths(normalized),
            Education: education,
            EducationCompleted: education is not null && completed,
            SkillsRequired: required,
            SkillsPreferred: preferred,
            PostedAt: posted,
            ExpiresAt: expires,
            ContentHash: TextNormalizer.ContentHash(title, company, district, description));

        return new NormalizeResult(job, null);

        static NormalizeResult Fail(string error) => new(null, error);
    }

    // ------------------------------------------------------------------ text cleanup

    private static string Clean(string? text, int max)
    {
        var decoded = WebUtility.HtmlDecode(HtmlTag().Replace(text ?? "", " "));
        var single = Whitespace().Replace(decoded, " ").Trim();
        return single.Length > max ? single[..max].TrimEnd() : single;
    }

    /// <summary>Strips HTML (feeds often embed it), keeps paragraph breaks, caps the size.</summary>
    public static string CleanDescription(string? html)
    {
        var withBreaks = BlockTag().Replace(html ?? "", "\n");
        var decoded = WebUtility.HtmlDecode(HtmlTag().Replace(withBreaks, " "));
        var lines = decoded.Split('\n').Select(l => Whitespace().Replace(l, " ").Trim()).Where(l => l.Length > 0);
        var text = string.Join('\n', lines);
        return text.Length > MaxDescriptionLength ? text[..MaxDescriptionLength].TrimEnd() + "…" : text;
    }

    // ------------------------------------------------------------------ location & modality

    public static WorkModality InferModality(string? text)
    {
        var n = TextNormalizer.Normalize(text);
        if (HasAny(n, "hibrido", "hibrida", "semipresencial", "semi presencial")) return WorkModality.Hybrid;
        if (HasAny(n, "remoto", "remota", "home office", "teletrabajo", "100 virtual", "trabajo virtual", "desde casa")) return WorkModality.Remote;
        return WorkModality.OnSite;
    }

    /// <summary>District from the location ("Los Olivos, Lima") or a title suffix ("Cajero - San Isidro").</summary>
    public static string? FindDistrict(string? location, string? title = null)
    {
        foreach (var candidate in Segments(location).Concat(Segments(title).Skip(1)))
        {
            if (LimaDistricts.Find(candidate) is { } district) return district.Name;
        }
        return null;

        static IEnumerable<string> Segments(string? text) =>
            string.IsNullOrWhiteSpace(text) ? [] : text.Split([',', '-', '/', '|', '(', ')', '–'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string CityFrom(string? location)
    {
        var first = location?.Split([',', '-', '/'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first is null || HasAny(TextNormalizer.Normalize(first), "remoto", "remota", "peru", "teletrabajo", "virtual")) return "Lima";
        return first.Length > 80 ? first[..80] : CultureInfo.GetCultureInfo("es-PE").TextInfo.ToTitleCase(first.ToLowerInvariant());
    }

    // ------------------------------------------------------------------ salary

    /// <summary>
    /// "S/ 1,800 - 2,200", "S/. 2500", "1800 soles", "Sueldo: 2 300". In free descriptions a currency marker is required so
    /// years, phone numbers or schedules are never read as money.
    /// </summary>
    public static (decimal? Min, decimal? Max)? ParseSalary(string? text, bool requireCurrency)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.ToLowerInvariant();

        foreach (Match m in SalaryRange().Matches(t))
        {
            if (requireCurrency && !m.Groups["cur"].Success && !m.Groups["soles"].Success && !m.Groups["soles1"].Success) continue;
            var a = Amount(m.Groups["a"].Value);
            var b = Amount(m.Groups["b"].Value);
            if (a is not null && b is not null) return (Math.Min(a.Value, b.Value), Math.Max(a.Value, b.Value));
        }

        foreach (Match m in SalarySingle().Matches(t))
        {
            if (requireCurrency && !m.Groups["cur"].Success && !m.Groups["soles"].Success) continue;
            if (Amount(m.Groups["a"].Value) is { } a)
                return HasAny(TextNormalizer.Normalize(t[..m.Index]).Split(' ').TakeLast(2), "hasta") ? (null, a) : (a, null);
        }
        return null;
    }

    private static decimal? Amount(string raw)
    {
        // Cents first ("2,500.00" → "2,500"), then thousands separators ("1,800" / "1.800" / "2 300" → digits only).
        var digits = CentsSuffix().Replace(raw.Replace(" ", ""), "").Replace(",", "").Replace(".", "");
        return decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? Sane(value) : null;
    }

    /// <summary>Monthly PEN salaries outside this band are almost always a parsing accident (a year, a phone).</summary>
    private static decimal? Sane(decimal? value) => value is >= 300 and <= 60_000 ? value : null;

    // ------------------------------------------------------------------ requirements

    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.Ordinal)
    {
        ["un"] = 1, ["una"] = 1, ["uno"] = 1, ["dos"] = 2, ["tres"] = 3, ["cuatro"] = 4, ["cinco"] = 5, ["seis"] = 6, ["medio"] = 0
    };

    /// <summary>Minimum experience in months; 0 when the offer says none is needed; null when it does not say.</summary>
    public static int? InferExperienceMonths(string normalized)
    {
        if (HasAny(normalized, "sin experiencia", "no requiere experiencia", "no se requiere experiencia", "no necesitas experiencia", "no es necesario experiencia"))
            return 0;

        var m = ExperienceBefore().Match(normalized);
        if (!m.Success) m = ExperienceAfter().Match(normalized);
        if (!m.Success) return null;

        var qty = m.Groups["n"].Value;
        var number = int.TryParse(qty, out var n) ? n : NumberWords.GetValueOrDefault(qty, -1);
        if (number < 0) return null;
        var months = m.Groups["unit"].Value.StartsWith("mes", StringComparison.Ordinal) ? number : number * 12;
        if (qty == "medio") months = 6;
        return months is >= 0 and <= 360 ? months : null;
    }

    /// <summary>Lowest level the offer accepts ("técnico o universitario" → técnico) and whether it must be finished.</summary>
    public static (EducationLevel? Level, bool Completed) InferEducation(string normalized)
    {
        EducationLevel? level = null;
        if (HasAny(normalized, "secundaria completa", "secundaria concluida", "5to de secundaria", "quinto de secundaria")) level = EducationLevel.Secondary;
        else if (HasAny(normalized, "estudios tecnicos", "tecnico completo", "tecnico titulado", "egresado tecnico", "formacion tecnica", "carrera tecnica", "tecnico o universitario", "tecnica o universitaria"))
            level = EducationLevel.Technical;
        else if (HasAny(normalized, "universitario", "universitaria", "bachiller", "licenciado", "licenciada", "titulado", "titulada"))
            level = EducationLevel.University;
        else if (HasAny(normalized, "maestria", "posgrado", "postgrado", "mba"))
            level = EducationLevel.Postgraduate;

        if (level is null) return (null, false);
        var inProgress = HasAny(normalized, "estudiante", "cursando", "ultimos ciclos", "ultimo ciclo", "en curso");
        var completed = !inProgress && HasAny(normalized, "titulado", "titulada", "egresado", "egresada", "bachiller", "completo", "completa", "concluido", "concluida");
        return (level, completed);
    }

    /// <summary>True for "lunes a viernes"; false when weekends or rotating shifts are mentioned; null when unknown.</summary>
    public static bool? InferWeekdaysOnly(string normalized)
    {
        if (HasAny(normalized, "lunes a sabado", "sabados", "sabado", "domingos", "domingo", "fines de semana", "fin de semana", "turnos rotativos", "rotativo"))
            return false;
        return HasAny(normalized, "lunes a viernes") ? true : null;
    }

    public static EmploymentType InferEmploymentType(string? text)
    {
        var n = TextNormalizer.Normalize(text);
        if (HasAny(n, "practicante", "practicas pre", "practicas profesionales", "pasantia", "internship")) return EmploymentType.Internship;
        if (HasAny(n, "medio tiempo", "part time", "tiempo parcial", "parttime")) return EmploymentType.PartTime;
        if (HasAny(n, "temporal", "por campana", "suplencia", "reemplazo")) return EmploymentType.Temporary;
        if (HasAny(n, "freelance", "por proyecto", "independiente")) return EmploymentType.Freelance;
        return EmploymentType.FullTime;
    }

    private static readonly string[] PreferredMarkers = ["deseable", "valorable", "se valora", "se valorara", "plus", "no indispensable", "de preferencia"];

    /// <summary>
    /// Structured skills when the source has them. Otherwise catalogue skills found in the text: those mentioned only in
    /// "deseable / se valora" sentences are preferred, the rest required. A level written next to the skill
    /// ("Excel intermedio") becomes the minimum level.
    /// </summary>
    private static (IReadOnlyList<SkillRequirement> Required, IReadOnlyList<SkillRequirement> Preferred) Skills(RawJob raw, string title, string description)
    {
        if (raw.SkillsRequired is not null || raw.SkillsPreferred is not null)
        {
            var req = (raw.SkillsRequired ?? []).Select(s => Requirement(s.Skill, s.MinLevel)).Where(s => s is not null).Cast<SkillRequirement>();
            var pref = (raw.SkillsPreferred ?? []).Select(s => Requirement(s, null)).Where(s => s is not null).Cast<SkillRequirement>();
            return (DistinctByKey(req), DistinctByKey(pref));
        }

        var sentences = SentenceSplit().Split($"{title}.\n{description}").Select(TextNormalizer.Normalize).Where(s => s.Length > 0).ToList();
        var required = new Dictionary<string, SkillRequirement>(StringComparer.Ordinal);
        var preferred = new Dictionary<string, SkillRequirement>(StringComparer.Ordinal);

        foreach (var sentence in sentences)
        {
            var isPreferred = HasAny(sentence, PreferredMarkers);
            foreach (var skill in SkillCatalog.DetectIn(sentence))
            {
                var target = isPreferred ? preferred : required;
                var level = LevelNear(sentence, skill);
                if (target.TryGetValue(skill.Key, out var existing))
                {
                    if (level is not null && (existing.MinLevel is null || level > existing.MinLevel)) existing.MinLevel = level;
                }
                else target[skill.Key] = new SkillRequirement { Key = skill.Key, Name = skill.Name, MinLevel = isPreferred ? null : level };
            }
        }

        foreach (var key in required.Keys) preferred.Remove(key);
        return (required.Values.ToList(), preferred.Values.ToList());
    }

    private static SkillLevel? LevelNear(string sentence, SkillDefinition skill)
    {
        foreach (var alias in skill.Aliases.Append(skill.Name).Select(TextNormalizer.Normalize).Where(a => a.Length > 2))
        {
            var m = Regex.Match($" {sentence} ", $@" {Regex.Escape(alias)}(?: a| de)?(?: nivel)? (?<lvl>basico|intermedio|avanzado)\b");
            if (m.Success)
                return m.Groups["lvl"].Value switch { "basico" => SkillLevel.Basic, "intermedio" => SkillLevel.Intermediate, _ => SkillLevel.Advanced };
        }
        return null;
    }

    private static SkillRequirement? Requirement(string skill, SkillLevel? level)
    {
        if (string.IsNullOrWhiteSpace(skill)) return null;
        var known = SkillCatalog.FindByKey(skill) ?? SkillCatalog.Resolve(skill);
        var name = known?.Name ?? Clean(skill, 80);
        return new SkillRequirement { Key = known?.Key ?? TextNormalizer.Normalize(name).Replace(' ', '-'), Name = name, MinLevel = level };
    }

    private static List<SkillRequirement> DistinctByKey(IEnumerable<SkillRequirement> skills) => skills.GroupBy(s => s.Key).Select(g => g.First()).ToList();

    private static bool HasAny(string normalized, params string[] phrases) => phrases.Any(p => $" {normalized} ".Contains($" {p} ", StringComparison.Ordinal));

    private static bool HasAny(IEnumerable<string> tokens, params string[] words) => tokens.Any(words.Contains);

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"<\s*(br|/p|/li|/div|/h\d|/ul|/ol)\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTag();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[.;\n!?]+")]
    private static partial Regex SentenceSplit();

    private const string AmountPattern = @"\d{1,3}(?:[.,\s]\d{3})+(?:[.,]\d{2})?|\d{3,6}(?:[.,]\d{2})?";

    [GeneratedRegex(@"(?<cur>s/\.?|pen\b)?\s*(?<a>" + AmountPattern + @")\s*(?<soles1>soles)?\s*(?:-|–|a|hasta|y)\s*(?:s/\.?|pen\b)?\s*(?<b>" + AmountPattern + @")\s*(?<soles>soles)?")]
    private static partial Regex SalaryRange();

    [GeneratedRegex(@"(?<cur>s/\.?|pen\b)\s*(?<a>" + AmountPattern + @")|(?<a>" + AmountPattern + @")\s*(?<soles>soles|nuevos soles)")]
    private static partial Regex SalarySingle();

    [GeneratedRegex(@"[.,]\d{1,2}$")]
    private static partial Regex CentsSuffix();

    [GeneratedRegex(@"(?<n>\d{1,2}|un|una|uno|dos|tres|cuatro|cinco|seis|medio)\s+(?<unit>anos?|meses?)\s+(?:de\s+)?experiencia")]
    private static partial Regex ExperienceBefore();

    [GeneratedRegex(@"experiencia\s+(?:laboral\s+)?(?:minima\s+)?(?:de\s+|no menor a\s+|mayor a\s+)?(?<n>\d{1,2}|un|una|uno|dos|tres|cuatro|cinco|seis|medio)\s+(?<unit>anos?|meses?)")]
    private static partial Regex ExperienceAfter();
}
