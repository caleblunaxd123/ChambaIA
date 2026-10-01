using System.Text.RegularExpressions;
using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Resumes;

public enum ResumeSection { Header, Profile, Experience, Education, Skills, Languages, Courses, Other }

/// <summary>Cleans extracted text into lines and splits it by the usual Spanish CV headings.</summary>
public static partial class ResumeSections
{
    private static readonly (ResumeSection Section, string[] Headings)[] Known =
    [
        (ResumeSection.Profile, ["perfil", "perfil profesional", "resumen", "resumen profesional", "objetivo", "objetivo profesional", "sobre mi", "presentacion", "datos personales"]),
        (ResumeSection.Experience, ["experiencia", "experiencia laboral", "experiencia profesional", "experiencia de trabajo", "historial laboral", "trayectoria laboral", "trayectoria profesional"]),
        (ResumeSection.Education, ["educacion", "formacion", "formacion academica", "estudios", "estudios realizados", "datos academicos", "informacion academica", "educacion y formacion"]),
        (ResumeSection.Skills, ["habilidades", "competencias", "conocimientos", "skills", "habilidades y competencias", "habilidades tecnicas", "conocimientos informaticos", "herramientas", "aptitudes"]),
        (ResumeSection.Languages, ["idiomas", "idioma", "lenguajes"]),
        (ResumeSection.Courses, ["cursos", "certificaciones", "certificados", "capacitaciones", "cursos y certificaciones", "cursos y capacitaciones", "formacion complementaria", "otros estudios", "seminarios"])
    ];

    private static readonly Dictionary<string, ResumeSection> HeadingMap = Known
        .SelectMany(k => k.Headings.Select(h => (Heading: h, k.Section)))
        .ToDictionary(x => x.Heading, x => x.Section);

    public static List<string> CleanLines(string text) =>
        text.Replace(' ', ' ')
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => MultiSpace().Replace(l.Trim(), " "))
            .Where(l => l.Length > 0)
            .ToList();

    public static Dictionary<ResumeSection, List<string>> Split(IReadOnlyList<string> lines)
    {
        var result = new Dictionary<ResumeSection, List<string>> { [ResumeSection.Header] = [] };
        var current = ResumeSection.Header;

        foreach (var line in lines)
        {
            if (TryHeading(line, out var section, out var inlineRest))
            {
                current = section;
                result.TryAdd(current, []);
                if (inlineRest.Length > 0) result[current].Add(inlineRest);
                continue;
            }
            result[current].Add(line);
        }
        return result;
    }

    /// <summary>"EXPERIENCIA LABORAL", "Experiencia laboral:", "Perfil: Asistente administrativa…".</summary>
    private static bool TryHeading(string line, out ResumeSection section, out string inlineRest)
    {
        section = ResumeSection.Other;
        inlineRest = "";

        var colon = line.IndexOf(':');
        var head = colon > 0 && colon < 40 ? line[..colon] : line;
        var normalized = TextNormalizer.Normalize(head);

        if (normalized.Length == 0 || normalized.Length > 40 || !HeadingMap.TryGetValue(normalized, out section)) return false;
        if (colon < 0 && line.Length > 40) return false;

        inlineRest = colon > 0 ? line[(colon + 1)..].Trim() : "";
        return true;
    }

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpace();
}
