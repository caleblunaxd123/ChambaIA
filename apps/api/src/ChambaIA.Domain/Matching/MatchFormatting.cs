using System.Globalization;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Matching;

/// <summary>Spanish wording helpers shared by the explanation builders.</summary>
public static class MatchFormatting
{
    public static string Duration(int months)
    {
        if (months <= 0) return "sin experiencia";
        var years = months / 12;
        var rest = months % 12;
        var y = years switch { 0 => "", 1 => "1 año", _ => $"{years} años" };
        var m = rest switch { 0 => "", 1 => "1 mes", _ => $"{rest} meses" };
        return y.Length > 0 && m.Length > 0 ? $"{y} y {m}" : y + m;
    }

    public static string Money(decimal amount) =>
        "S/ " + amount.ToString("N0", CultureInfo.InvariantCulture);

    public static string SalaryRange(decimal? min, decimal? max) => (min, max) switch
    {
        (null, null) => "",
        ({ } a, { } b) when a == b => Money(a),
        ({ } a, { } b) => $"{Money(a)} - {Money(b)}",
        ({ } a, null) => $"Desde {Money(a)}",
        (null, { } b) => $"Hasta {Money(b)}"
    };

    public static string Level(SkillLevel level) => level switch
    {
        SkillLevel.Basic => "básico",
        SkillLevel.Intermediate => "intermedio",
        _ => "avanzado"
    };

    public static string Education(EducationLevel level, bool completed) => level switch
    {
        EducationLevel.Secondary => completed ? "secundaria completa" : "secundaria",
        EducationLevel.Technical => completed ? "estudios técnicos completos" : "estudios técnicos",
        EducationLevel.University => completed ? "estudios universitarios completos" : "estudios universitarios",
        _ => completed ? "posgrado completo" : "posgrado"
    };

    public static string Category(MatchCategory category) => category switch
    {
        MatchCategory.Excellent => "Excelente opción",
        MatchCategory.VeryCompatible => "Muy compatible",
        MatchCategory.Compatible => "Compatible",
        MatchCategory.Review => "Revisar",
        _ => "Poco compatible"
    };

    /// <summary>Honest one-line verdict. Never claims more certainty than the data supports.</summary>
    public static string Recommendation(MatchCategory category) => category switch
    {
        MatchCategory.Excellent => "Encaja muy bien con tu perfil y con lo que buscas.",
        MatchCategory.VeryCompatible => "La oferta cumple la mayoría de tus criterios.",
        MatchCategory.Compatible => "Cumples buena parte de lo que piden. Revisa los puntos marcados antes de postular.",
        MatchCategory.Review => "No es claro que cumplas todos los requisitos. Revísala con calma antes de decidir.",
        _ => "No te la recomendamos: choca con algo que pediste evitar o con requisitos importantes."
    };
}
