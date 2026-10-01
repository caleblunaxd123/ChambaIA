using System.Text.RegularExpressions;
using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Skills;
using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Resumes;

/// <summary>
/// Deterministic CV understanding: extracted text in, structured proposal out. No AI, no cost, same input → same output.
/// An optional cheap-LLM enrichment (phase 8) can only *add* to this result and must validate against the same schema.
/// </summary>
public static partial class ResumeParser
{
    public const int MinTextLength = 80;

    public static ParsedResume Parse(string text, DateOnly today)
    {
        var lines = ResumeSections.CleanLines(text);
        var sections = ResumeSections.Split(lines);
        var result = new ParsedResume();

        List<string> Section(ResumeSection s) => sections.GetValueOrDefault(s) ?? [];

        // Experience: prefer its own section; without headings fall back to every line outside education.
        var experienceLines = sections.ContainsKey(ResumeSection.Experience)
            ? Section(ResumeSection.Experience)
            : lines.Except(Section(ResumeSection.Education)).ToList();
        var experience = ExperienceParser.Parse(experienceLines, today);
        result.Experience = experience.Entries;
        result.ExperienceMonths = experience.TotalMonths;
        if (experience.Entries.Count == 0)
            result.Warnings.Add("No pudimos detectar tu experiencia laboral con fechas. Revisa o completa tu experiencia total.");

        // Education.
        if (sections.ContainsKey(ResumeSection.Education))
        {
            var education = EducationParser.Parse(Section(ResumeSection.Education), today.Year);
            result.Education = education.Entries;
            result.EducationLevel = education.HighestLevel;
            result.EducationStatus = education.HighestStatus;
        }
        if (result.Education.Count == 0)
            result.Warnings.Add("No pudimos detectar tus estudios. Indica tu nivel educativo más alto.");

        // Skills: catalogue skills anywhere in the text, with the level the CV itself states.
        result.Skills = DetectSkills(lines, Section(ResumeSection.Experience), Section(ResumeSection.Skills));
        if (result.Skills.Count == 0)
            result.Warnings.Add("No reconocimos habilidades en tu CV. Agrégalas para que tu agente pueda compararte con las ofertas.");
        else
            result.Warnings.Add("Los niveles de tus habilidades son una estimación. Ajústalos si no reflejan lo que sabes hacer.");

        // Languages & courses.
        result.Languages = sections.ContainsKey(ResumeSection.Languages)
            ? LanguageParser.Parse(Section(ResumeSection.Languages), requireLevel: false)
            : LanguageParser.Parse(lines, requireLevel: true);
        result.Certifications = Section(ResumeSection.Courses)
            .Select(l => l.TrimStart('•', '▪', '●', '·', '-', '–', '*', ' '))
            .Where(l => l.Length is >= 4 and <= 150)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .ToList();

        result.FullNameGuess = GuessName(Section(ResumeSection.Header));
        result.Headline = GuessHeadline(Section(ResumeSection.Profile), result.Experience);
        result.SuggestedRoles = RoleSuggester.Suggest(result.Experience, result.Headline, result.Skills);
        return result;
    }

    private static List<ProfileSkill> DetectSkills(List<string> lines, List<string> experienceLines, List<string> skillLines)
    {
        var practiced = experienceLines.SelectMany(l => SkillCatalog.DetectIn(l)).Select(s => s.Key).ToHashSet();
        var levels = new Dictionary<string, SkillLevel?>();
        var found = new Dictionary<string, SkillDefinition>();

        foreach (var line in lines)
        {
            var mentioned = SkillCatalog.DetectIn(line);
            if (mentioned.Count == 0) continue;
            var stated = StatedLevel(TextNormalizer.Normalize(line));
            foreach (var skill in mentioned)
            {
                found[skill.Key] = skill;
                if (stated is not null && (levels.GetValueOrDefault(skill.Key) is null || stated > levels[skill.Key])) levels[skill.Key] = stated;
            }
        }

        var skills = found.Values
            .Select(s => new ProfileSkill
            {
                Key = s.Key,
                Name = s.Name,
                // Unstated level: practiced at work → intermediate; merely listed → basic (never overstate).
                Level = levels.GetValueOrDefault(s.Key) ?? (practiced.Contains(s.Key) ? SkillLevel.Intermediate : SkillLevel.Basic)
            })
            .ToList();

        // Custom skills the user wrote in the skills section that are not in the catalogue.
        foreach (var item in SkillItems(skillLines))
        {
            if (SkillCatalog.Resolve(item) is not null || SkillCatalog.DetectIn(item).Count > 0) continue;
            var key = TextNormalizer.Normalize(item).Replace(' ', '-');
            if (key.Length == 0 || skills.Any(s => s.Key == key)) continue;
            skills.Add(new ProfileSkill { Key = key, Name = item, Level = StatedLevel(TextNormalizer.Normalize(item)) ?? SkillLevel.Basic });
        }

        return skills.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).Take(40).ToList();
    }

    private static IEnumerable<string> SkillItems(List<string> skillLines) =>
        skillLines
            .SelectMany(l => ItemSeparator().Split(l))
            .Select(i => LevelSuffix().Replace(i, "").Trim(' ', '•', '▪', '●', '·', '-', '–', '*', '.', ':'))
            .Where(i => i.Length is >= 3 and <= 40 && i.Split(' ').Length <= 4 && !i.Any(char.IsDigit))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15);

    private static SkillLevel? StatedLevel(string normalizedLine) => normalizedLine switch
    {
        _ when HasWord(normalizedLine, "avanzado", "alto", "experto", "dominio") => SkillLevel.Advanced,
        _ when HasWord(normalizedLine, "intermedio", "medio") => SkillLevel.Intermediate,
        _ when HasWord(normalizedLine, "basico", "principiante", "elemental") => SkillLevel.Basic,
        _ => null
    };

    private static bool HasWord(string text, params string[] words) =>
        words.Any(w => $" {text} ".Contains($" {w} ", StringComparison.Ordinal));

    private static string? GuessName(List<string> header)
    {
        var first = header.FirstOrDefault();
        if (first is null) return null;
        var words = first.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var looksLikeName = words.Length is >= 2 and <= 5
            && words.All(w => w.All(c => char.IsLetter(c) || c is '\'' or '.' or '-') && char.IsUpper(w[0]))
            && !first.Contains('@');
        return looksLikeName ? TitleCase(first) : null;
    }

    private static string? GuessHeadline(List<string> profile, List<WorkExperience> experience)
    {
        var sentence = profile.FirstOrDefault();
        if (sentence is not null)
        {
            var cut = sentence.IndexOfAny(['.', ';']);
            var head = (cut > 10 ? sentence[..cut] : sentence).Trim();
            if (head.Length is >= 8 and <= 160) return head;
        }

        var latest = experience.OrderByDescending(e => e.EndDate is null ? DateOnly.MaxValue : e.EndDate.Value).FirstOrDefault();
        return latest is null || latest.Title.Length == 0 ? null : TitleCase(latest.Title);
    }

    public static string TitleCase(string text)
    {
        var small = new HashSet<string> { "de", "del", "la", "las", "el", "los", "y", "e", "en", "al", "para", "con" };
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select((w, i) => i > 0 && small.Contains(w.ToLowerInvariant()) ? w.ToLowerInvariant() : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant());
        return string.Join(' ', words);
    }

    [GeneratedRegex(@"[,;•▪●·\n]|\s[-–]\s")]
    private static partial Regex ItemSeparator();

    [GeneratedRegex(@"\(?\b(?:básico|basico|intermedio|avanzado)\b\)?", RegexOptions.IgnoreCase)]
    private static partial Regex LevelSuffix();
}

/// <summary>Maps what the candidate has done to the job titles they will probably want to search for.</summary>
public static class RoleSuggester
{
    private static readonly (string[] Triggers, string[] Roles)[] Families =
    [
        (["administrativ", "asistente", "auxiliar", "secretar", "recepcion", "oficina", "back office", "operaciones"], ["Asistente Administrativo", "Auxiliar Administrativo", "Back Office"]),
        (["academic", "colegio", "instituto", "educacion", "docente"], ["Asistente Académico"]),
        (["ventas", "vendedor", "comercial", "asesor"], ["Asesor de Ventas"]),
        (["almacen", "logistic", "inventario", "despacho"], ["Asistente de Logística"]),
        (["recursos humanos", "rrhh", "reclutamiento"], ["Asistente de Recursos Humanos"]),
        (["contab", "tesoreria"], ["Asistente Contable"])
    ];

    private static readonly Dictionary<string, string> BySkill = new()
    {
        ["facturacion"] = "Facturación",
        ["atencion-al-cliente"] = "Atención al Cliente",
        ["gestion-documentaria"] = "Gestión Documentaria",
        ["compras"] = "Asistente de Compras",
        ["cobranzas"] = "Cobranzas"
    };

    public static List<string> Suggest(IEnumerable<WorkExperience> experience, string? headline, IEnumerable<ProfileSkill> skills)
    {
        var titles = experience.Select(e => ResumeParser.TitleCase(e.Title)).Where(t => t.Length is > 2 and <= 60).ToList();
        var haystack = TextNormalizer.Normalize(string.Join(' ', titles.Append(headline ?? "")));

        var roles = new List<string>(titles.Take(3));
        foreach (var (triggers, family) in Families)
            if (triggers.Any(t => haystack.Contains(t, StringComparison.Ordinal))) roles.AddRange(family);
        foreach (var skill in skills)
            if (BySkill.TryGetValue(skill.Key, out var role)) roles.Add(role);

        return roles.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
    }
}
