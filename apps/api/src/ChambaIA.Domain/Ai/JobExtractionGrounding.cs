using System.Globalization;
using System.Text.RegularExpressions;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Ai;

/// <summary>
/// A model can answer with something well-formed and plausible that the offer never said (measured with real local models:
/// "S/ 1,100 semanales" came back as 11000 a month, and as 1100 a month). Grounding checks every value against the offer text
/// itself, with plain rules, and drops whatever cannot be found there. It would rather leave a field empty than keep a guess:
/// a wrong salary or requirement hides good offers from people or shows them bad ones.
/// </summary>
public static partial class JobExtractionGrounding
{
    private static readonly string[] NotMonthly =
    [
        "semanal", "quincenal", "por hora", "x hora", "la hora", "por dia", "al dia", "jornal", "a la semana", "por semana", "anual", "al ano", "por ano"
    ];

    private static readonly Dictionary<string, int> Words = new()
    {
        ["un"] = 1, ["una"] = 1, ["dos"] = 2, ["tres"] = 3, ["cuatro"] = 4, ["cinco"] = 5, ["seis"] = 6, ["siete"] = 7, ["ocho"] = 8, ["nueve"] = 9, ["diez"] = 10
    };

    public static JobExtractionResult Ground(JobExtractionResult result, string description)
    {
        var text = TextNormalizer.Normalize(description);
        var numbers = NumbersIn(description);
        var monthly = !NotMonthly.Any(p => text.Contains(p, StringComparison.Ordinal));

        var min = monthly && FoundIn(result.SalaryMin, numbers) ? result.SalaryMin : null;
        var max = monthly && FoundIn(result.SalaryMax, numbers) ? result.SalaryMax : null;

        return result with
        {
            SalaryMin = min,
            SalaryMax = max,
            ExperienceMonths = ExperienceFoundIn(result.ExperienceMonths, text) ? result.ExperienceMonths : null,
            WeekdaysOnly = WeekdaysFoundIn(result.WeekdaysOnly, text) ? result.WeekdaysOnly : null,
            Education = EducationFoundIn(result.Education, text) ? result.Education : null
        };
    }

    private static bool FoundIn(decimal? salary, IReadOnlyList<decimal> numbers) =>
        salary is { } s && numbers.Any(n => Math.Abs(n - s) < 1m);

    /// <summary>All the numbers written in the text, understanding "1,800", "1.800", "1 800" and "1800.50".</summary>
    private static List<decimal> NumbersIn(string text)
    {
        var found = new List<decimal>();
        foreach (Match m in Number().Matches(text))
        {
            var token = m.Value.Trim('.', ',', ' ');
            var thousands = Thousands().Match(token);
            var cleaned = thousands.Success
                ? string.Concat(thousands.Groups[1].Value.Where(char.IsDigit)) + (thousands.Groups[2].Success ? "." + thousands.Groups[2].Value : "")
                : token.Replace(',', '.');
            if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) found.Add(value);
        }
        return found;
    }

    private static bool ExperienceFoundIn(int? months, string text)
    {
        if (months is null) return false;
        if (months == 0) return text.Contains("sin experiencia", StringComparison.Ordinal) || text.Contains("no se requiere experiencia", StringComparison.Ordinal)
                             || text.Contains("no requiere experiencia", StringComparison.Ordinal) || text.Contains("no necesita experiencia", StringComparison.Ordinal);

        var candidates = new HashSet<int>();
        foreach (Match m in Duration().Matches(text))
        {
            var raw = m.Groups[1].Value;
            var n = int.TryParse(raw, out var digits) ? digits : Words.GetValueOrDefault(raw);
            if (n > 0) candidates.Add(m.Groups[2].Value.StartsWith("mes", StringComparison.Ordinal) ? n : n * 12);
        }
        if (text.Contains("ano y medio", StringComparison.Ordinal)) candidates.Add(18);
        if (text.Contains("medio ano", StringComparison.Ordinal)) candidates.Add(6);
        return candidates.Contains(months.Value);
    }

    private static bool WeekdaysFoundIn(bool? weekdaysOnly, string text) => weekdaysOnly switch
    {
        true => text.Contains("lunes a viernes", StringComparison.Ordinal) || text.Contains("lun a vie", StringComparison.Ordinal),
        false => text.Contains("sabado", StringComparison.Ordinal) || text.Contains("domingo", StringComparison.Ordinal) || text.Contains("fin de semana", StringComparison.Ordinal) || text.Contains("fines de semana", StringComparison.Ordinal),
        _ => false
    };

    private static bool EducationFoundIn(EducationLevel? level, string text) => level switch
    {
        EducationLevel.Secondary => text.Contains("secundaria", StringComparison.Ordinal) || text.Contains("colegio", StringComparison.Ordinal),
        EducationLevel.Technical => text.Contains("tecnic", StringComparison.Ordinal) || text.Contains("instituto", StringComparison.Ordinal) || text.Contains("cetpro", StringComparison.Ordinal),
        EducationLevel.University => text.Contains("universitari", StringComparison.Ordinal) || text.Contains("bachiller", StringComparison.Ordinal) || text.Contains("licenciad", StringComparison.Ordinal)
                                     || text.Contains("titulad", StringComparison.Ordinal) || text.Contains("egresad", StringComparison.Ordinal) || text.Contains("profesional", StringComparison.Ordinal),
        EducationLevel.Postgraduate => text.Contains("maestria", StringComparison.Ordinal) || text.Contains("postgrado", StringComparison.Ordinal) || text.Contains("posgrado", StringComparison.Ordinal)
                                      || text.Contains("doctorado", StringComparison.Ordinal) || text.Contains("mba", StringComparison.Ordinal),
        _ => false
    };

    [GeneratedRegex(@"\d[\d.,]*")]
    private static partial Regex Number();

    // "1,800" / "1.800" / "1,800.50" / "12.345,67": groups of exactly three digits are thousands, a trailing 1-2 digit group is decimals.
    [GeneratedRegex(@"^(\d{1,3}(?:[.,]\d{3})+)(?:[.,](\d{1,2}))?$")]
    private static partial Regex Thousands();

    [GeneratedRegex(@"\b(\d{1,2}|un|una|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez)\s+(anos?|meses|mes)\b")]
    private static partial Regex Duration();
}
