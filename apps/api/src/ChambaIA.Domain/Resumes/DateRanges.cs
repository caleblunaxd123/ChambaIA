using System.Text.RegularExpressions;

namespace ChambaIA.Domain.Resumes;

/// <summary>A period found in CV text. <see cref="End"/> is null when it is still ongoing ("Actualidad").</summary>
public readonly record struct DateSpan(DateOnly Start, DateOnly? End, int MatchIndex, int MatchLength)
{
    public int MonthsUntil(DateOnly now)
    {
        var end = End ?? now;
        return Math.Max(1, (end.Year * 12 + end.Month) - (Start.Year * 12 + Start.Month) + 1);
    }
}

/// <summary>Finds "ene 2022 - dic 2023", "01/2023 – Actualidad", "2019 - 2021"… in a line of a Spanish CV.</summary>
public static partial class DateRanges
{
    private const string Months = @"ene(?:ro)?|feb(?:rero)?|mar(?:zo)?|abr(?:il)?|may(?:o)?|jun(?:io)?|jul(?:io)?|ago(?:sto)?|set(?:iembre)?|sep(?:t(?:iembre)?)?|oct(?:ubre)?|nov(?:iembre)?|dic(?:iembre)?";

    private static string Date(string s) =>
        $@"(?:(?<m{s}>{Months})\.?\s*(?:de\s+|del\s+|,\s*)?(?<y{s}>(?:19|20)\d{{2}})|(?<n{s}>\d{{1,2}})\s*[/\-.]\s*(?<ny{s}>(?:19|20)\d{{2}})|(?<o{s}>(?:19|20)\d{{2}}))";

    private const string Present = @"actualidad|actual|presente|hoy|la fecha|a la fecha|en curso|actualmente";

    private static readonly Regex Range = new(
        $@"\b{Date("1")}\s*(?:-|–|—|a|al|hasta)\s*(?:{Date("2")}|(?<now>{Present}))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Single = new($@"\b{Date("1")}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static DateSpan? Find(string line)
    {
        var m = Range.Match(line);
        if (!m.Success) return null;

        var start = ToDate(m, "1", endOfPeriod: false);
        if (start is null) return null;

        DateOnly? end = m.Groups["now"].Success ? null : ToDate(m, "2", endOfPeriod: true);
        if (!m.Groups["now"].Success && end is null) return null;
        if (end is { } e && e < start) return null;

        return new DateSpan(start.Value, end, m.Index, m.Length);
    }

    /// <summary>The last year mentioned in a line (education end dates, "2018 - 2020", "Egresada 2021").</summary>
    public static int? LastYear(string line)
    {
        var years = YearRegex().Matches(line).Select(m => int.Parse(m.Value)).ToList();
        return years.Count == 0 ? null : years.Max();
    }

    [GeneratedRegex(@"\b(?:19|20)\d{2}\b")]
    private static partial Regex YearRegex();

    private static DateOnly? ToDate(Match m, string s, bool endOfPeriod)
    {
        if (m.Groups[$"m{s}"].Success)
            return Make(int.Parse(m.Groups[$"y{s}"].Value), MonthNumber(m.Groups[$"m{s}"].Value));
        if (m.Groups[$"n{s}"].Success)
        {
            var month = int.Parse(m.Groups[$"n{s}"].Value);
            return month is >= 1 and <= 12 ? Make(int.Parse(m.Groups[$"ny{s}"].Value), month) : null;
        }
        if (m.Groups[$"o{s}"].Success)
            return Make(int.Parse(m.Groups[$"o{s}"].Value), endOfPeriod ? 12 : 1);
        return null;
    }

    private static DateOnly Make(int year, int month) => new(year, month, 1);

    private static int MonthNumber(string name) => name.ToLowerInvariant()[..3] switch
    {
        "ene" => 1, "feb" => 2, "mar" => 3, "abr" => 4, "may" => 5, "jun" => 6,
        "jul" => 7, "ago" => 8, "set" or "sep" => 9, "oct" => 10, "nov" => 11, _ => 12
    };

    /// <summary>Total months covered by the spans, counting overlaps once.</summary>
    public static int TotalMonths(IEnumerable<(DateOnly Start, DateOnly? End)> spans, DateOnly now)
    {
        var intervals = spans
            .Select(s => (Start: s.Start.Year * 12 + s.Start.Month, End: (s.End ?? now).Year * 12 + (s.End ?? now).Month))
            .OrderBy(i => i.Start)
            .ToList();

        var total = 0;
        var cursor = int.MinValue;
        foreach (var (start, end) in intervals)
        {
            var from = Math.Max(start, cursor + 1);
            if (end >= from) total += end - from + 1;
            cursor = Math.Max(cursor, end);
        }
        return total;
    }
}
