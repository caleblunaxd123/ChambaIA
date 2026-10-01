using System.Text.RegularExpressions;
using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Resumes;

/// <summary>Turns the lines of the "Experiencia" section into job entries anchored on their date ranges.</summary>
public static partial class ExperienceParser
{
    private static readonly string[] TitleWords =
    [
        "asistente", "auxiliar", "analista", "coordinador", "coordinadora", "practicante", "recepcionista", "jefe", "jefa", "ejecutivo", "ejecutiva",
        "vendedor", "vendedora", "cajero", "cajera", "operario", "operaria", "tecnico", "tecnica", "secretaria", "secretario", "administrador",
        "administradora", "supervisor", "supervisora", "encargado", "encargada", "asesor", "asesora", "gerente", "digitador", "digitadora",
        "almacenero", "promotor", "promotora", "docente", "profesor", "profesora", "contador", "contadora", "especialista", "responsable", "apoyo", "pasante", "trainee"
    ];

    private static readonly char[] Bullets = ['•', '▪', '●', '◦', '·', '-', '–', '*', '', '○', '■'];

    public sealed record Result(List<WorkExperience> Entries, int TotalMonths);

    public static Result Parse(IReadOnlyList<string> lines, DateOnly now)
    {
        // 1. Anchors: every line that carries a date range starts an entry.
        var anchors = new List<(int Index, DateSpan Span)>();
        for (var i = 0; i < lines.Count; i++)
            if (DateRanges.Find(lines[i]) is { } span) anchors.Add((i, span));

        var headers = anchors.Select((a, k) => HeaderStart(lines, a.Index, k == 0 ? 0 : anchors[k - 1].Index + 1)).ToList();

        var entries = new List<WorkExperience>();
        var spans = new List<(DateOnly, DateOnly?)>();

        for (var k = 0; k < anchors.Count; k++)
        {
            var (index, span) = anchors[k];
            var headerLines = new List<string>();
            for (var i = headers[k]; i < index; i++) headerLines.Add(Strip(lines[i]));

            var sameLine = Strip(RemoveRange(lines[index], span));
            if (sameLine.Length > 0) headerLines.Add(sameLine);
            else if (headerLines.Count == 0 && index + 1 < lines.Count && !IsBullet(lines[index + 1]) && DateRanges.Find(lines[index + 1]) is null)
                headerLines.Add(Strip(lines[index + 1])); // "ene 2023 - actual" followed by the title

            var (title, company) = Classify(headerLines);
            if (title.Length == 0 && company.Length == 0) continue;

            var descriptionEnd = k + 1 < anchors.Count ? headers[k + 1] : lines.Count;
            var description = string.Join(' ', lines.Skip(index + 1).Take(Math.Max(0, descriptionEnd - index - 1)).Select(Strip).Where(l => l.Length > 0));

            entries.Add(new WorkExperience
            {
                Title = title.Length > 0 ? title : company,
                Company = title.Length > 0 ? company : "",
                StartDate = span.Start,
                EndDate = span.End,
                Description = description.Length == 0 ? null : description.Length > 600 ? description[..600] : description
            });
            spans.Add((span.Start, span.End));
        }

        return new Result(entries, DateRanges.TotalMonths(spans, now));
    }

    /// <summary>Up to two non-bullet lines right above the date line belong to the entry header.</summary>
    private static int HeaderStart(IReadOnlyList<string> lines, int anchor, int floor)
    {
        var start = anchor;
        for (var i = anchor - 1; i >= floor && anchor - i <= 2; i--)
        {
            if (IsBullet(lines[i]) || DateRanges.Find(lines[i]) is not null || lines[i].Length > 90) break;
            start = i;
        }
        return start;
    }

    private static (string Title, string Company) Classify(List<string> candidates)
    {
        var parts = candidates
            .SelectMany(Split)
            .Where(p => p.Length > 1)
            .ToList();

        var title = parts.FirstOrDefault(LooksLikeTitle) ?? "";
        var company = parts.FirstOrDefault(p => p != title) ?? "";

        if (title.Length == 0 && parts.Count >= 2) (title, company) = (parts[0], parts[1]);
        else if (title.Length == 0 && parts.Count == 1) title = parts[0];
        return (Trim(title), Trim(company));
    }

    private static IEnumerable<string> Split(string line)
    {
        // "Asistente administrativa | Colegio X", "Asistente - Colegio X", "Asistente en Colegio X", "Asistente, Colegio X"
        var pieces = SeparatorRegex().Split(line).Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        return pieces.Count == 0 ? [line] : pieces;
    }

    private static bool LooksLikeTitle(string text) =>
        TextNormalizer.Normalize(text).Split(' ').Any(w => TitleWords.Contains(w));

    private static string RemoveRange(string line, DateSpan span) =>
        line.Remove(span.MatchIndex, span.MatchLength);

    private static bool IsBullet(string line) => line.Length > 0 && Bullets.Contains(line[0]) && line.Length > 1 && line[1] == ' ';

    private static string Strip(string line) => Trim(line.TrimStart(Bullets).Trim());

    private static string Trim(string s) => s.Trim(' ', '|', '-', '–', '—', ',', '(', ')', ':', ';');

    [GeneratedRegex(@"\s*[|•·]\s*|\s+[-–—]\s+|\s*,\s+|\s+(?:en|@)\s+")]
    private static partial Regex SeparatorRegex();
}
