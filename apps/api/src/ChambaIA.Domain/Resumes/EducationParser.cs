using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Resumes;

public static class EducationParser
{
    private static readonly (EducationLevel Level, string[] Keywords)[] Levels =
    [
        (EducationLevel.Postgraduate, ["maestria", "magister", "doctorado", "posgrado", "postgrado", "mba", "especializacion"]),
        (EducationLevel.University, ["universidad", "universitari", "licenciatura", "licenciado", "licenciada", "bachiller", "ingenieria", "ingeniero", "ingeniera", "pregrado", "carrera profesional"]),
        (EducationLevel.Technical, ["tecnic", "instituto", "tecnologic", "senati", "tecsup", "cibertec", "idat", "carrera tecnica", "profesional tecnico"]),
        (EducationLevel.Secondary, ["secundaria", "colegio", "educacion basica"])
    ];

    private static readonly string[] InstitutionWords =
        ["universidad", "instituto", "colegio", "senati", "tecsup", "cibertec", "idat", "upc", "pucp", "unmsm", "uni ", "ulima", "usil", "escuela", "centro de estudios"];

    private static readonly string[] InProgressWords = ["en curso", "cursando", "cursa", "estudiando", "ciclo", "actualmente", "actualidad", "presente", "trunca"];
    private static readonly string[] CompletedWords = ["egresad", "titulad", "bachiller", "culminad", "concluid", "completa", "completo", "terminad", "graduad", "finalizad"];

    public sealed record Result(List<EducationEntry> Entries, EducationLevel? HighestLevel, EducationStatus? HighestStatus);

    public static Result Parse(IReadOnlyList<string> lines, int currentYear)
    {
        var entries = new List<EducationEntry>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var level = LevelOf(line);
            if (level is null) continue;

            var institution = InstitutionIn(line) ?? Neighbour(lines, i, -1) ?? Neighbour(lines, i, +1) ?? "";
            var context = string.Join(' ', Window(lines, i));

            entries.Add(new EducationEntry
            {
                Degree = Clean(DateRanges.Find(line) is { } s ? line.Remove(s.MatchIndex, s.MatchLength) : line),
                Institution = Clean(institution),
                Level = level.Value,
                Status = StatusOf(context, currentYear)
            });
        }

        // The same degree can match twice ("Instituto X" line + "Técnico en Y" line): keep the richer entry per level+institution.
        var distinct = entries
            .GroupBy(e => (e.Level, Key: TextNormalizer.Normalize(e.Institution.Length > 0 ? e.Institution : e.Degree)))
            .Select(g => g.OrderByDescending(e => e.Degree.Length + e.Institution.Length).First())
            .ToList();

        var highest = distinct.OrderByDescending(e => e.Level).ThenByDescending(e => e.Status == EducationStatus.Completed).FirstOrDefault();
        return new Result(distinct, highest?.Level, highest?.Status);
    }

    private static EducationLevel? LevelOf(string line)
    {
        var text = TextNormalizer.Normalize(line);
        foreach (var (level, keywords) in Levels)
            if (keywords.Any(k => text.Contains(k, StringComparison.Ordinal))) return level;
        return null;
    }

    private static string? InstitutionIn(string line)
    {
        var text = TextNormalizer.Normalize(line) + " ";
        return InstitutionWords.Any(w => text.Contains(w, StringComparison.Ordinal)) ? line : null;
    }

    private static string? Neighbour(IReadOnlyList<string> lines, int index, int step)
    {
        var j = index + step;
        return j >= 0 && j < lines.Count && InstitutionIn(lines[j]) is { } found && LevelOf(lines[j]) is null ? found : null;
    }

    /// <summary>The degree line plus the next line when it only adds detail (dates, "egresada"), never a neighbouring degree.</summary>
    private static IEnumerable<string> Window(IReadOnlyList<string> lines, int i)
    {
        yield return lines[i];
        if (i + 1 < lines.Count && LevelOf(lines[i + 1]) is null && InstitutionIn(lines[i + 1]) is null) yield return lines[i + 1];
    }

    private static EducationStatus StatusOf(string context, int currentYear)
    {
        var text = TextNormalizer.Normalize(context);
        if (InProgressWords.Any(w => text.Contains(w, StringComparison.Ordinal)) && !CompletedWords.Any(w => text.Contains(w, StringComparison.Ordinal)))
            return EducationStatus.InProgress;
        if (CompletedWords.Any(w => text.Contains(w, StringComparison.Ordinal))) return EducationStatus.Completed;

        // A finished period in the past (or any past year) reads as completed.
        var year = DateRanges.LastYear(context);
        return year is not null && year <= currentYear && !text.Contains("actual") ? EducationStatus.Completed : EducationStatus.InProgress;
    }

    private static string Clean(string s) => s.Trim(' ', '|', '-', '–', '—', ',', '(', ')', ':', ';', '•', '·');
}

public static class LanguageParser
{
    private static readonly (string Name, string[] Aliases)[] Languages =
    [
        ("Español", ["espanol", "castellano"]),
        ("Inglés", ["ingles", "english"]),
        ("Portugués", ["portugues"]),
        ("Francés", ["frances"]),
        ("Italiano", ["italiano"]),
        ("Alemán", ["aleman"]),
        ("Quechua", ["quechua"]),
        ("Chino", ["chino", "mandarin"])
    ];

    public static string? LevelWord(string normalizedLine) => normalizedLine switch
    {
        _ when Has(normalizedLine, "nativo", "materno", "bilingue", "lengua materna") => "Nativo",
        _ when Has(normalizedLine, "avanzado", "fluido", "c1", "c2", "alto") => "Avanzado",
        _ when Has(normalizedLine, "intermedio", "medio", "b1", "b2", "regular") => "Intermedio",
        _ when Has(normalizedLine, "basico", "a1", "a2", "principiante", "elemental") => "Básico",
        _ => null
    };

    public static List<LanguageSkill> Parse(IEnumerable<string> lines, bool requireLevel)
    {
        var found = new Dictionary<string, LanguageSkill>();
        foreach (var line in lines)
        {
            var text = TextNormalizer.Normalize(line);
            foreach (var (name, aliases) in Languages)
            {
                if (!aliases.Any(a => $" {text} ".Contains($" {a} ", StringComparison.Ordinal))) continue;
                var level = LevelWord(text);
                if (level is null && requireLevel) continue;
                found.TryAdd(name, new LanguageSkill { Name = name, Level = level ?? "" });
            }
        }
        return [.. found.Values];
    }

    private static bool Has(string text, params string[] words) =>
        words.Any(w => $" {text} ".Contains($" {w} ", StringComparison.Ordinal));
}
