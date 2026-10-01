using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ChambaIA.Domain.Text;

/// <summary>Deterministic Spanish-friendly text normalisation used by dedup, matching and skill detection.</summary>
public static class TextNormalizer
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "de", "del", "la", "las", "el", "los", "y", "e", "o", "u", "en", "para", "con", "a", "al", "por", "un", "una", "se", "su", "sus"
    };

    // Common title synonyms collapsed to one token so "Auxiliar" and "Asistente" compare as equal.
    private static readonly Dictionary<string, string> Synonyms = new(StringComparer.Ordinal)
    {
        ["auxiliar"] = "asistente",
        ["ayudante"] = "asistente",
        ["apoyo"] = "asistente",
        ["practicante"] = "practicante"
    };

    /// <summary>Lowercase, no diacritics, only letters/digits separated by single spaces.</summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        var decomposed = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var lastWasSpace = true;

        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                sb.Append(' ');
                lastWasSpace = true;
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Normalised tokens without stop words, with gender/number endings stripped.</summary>
    public static IReadOnlyList<string> Stems(string? input) =>
        Normalize(input)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !StopWords.Contains(t))
            .Select(t => Synonyms.TryGetValue(t, out var s) ? s : t)
            .Select(Stem)
            .ToList();

    public static string Stem(string token)
    {
        if (token.Length <= 4) return token;
        foreach (var suffix in new[] { "as", "os", "a", "o", "s" })
        {
            if (token.EndsWith(suffix, StringComparison.Ordinal) && token.Length - suffix.Length >= 4)
                return token[..^suffix.Length];
        }
        return token;
    }

    /// <summary>True when the normalised haystack contains the normalised needle as whole words.</summary>
    public static bool ContainsPhrase(string? haystack, string? needle)
    {
        var h = Normalize(haystack);
        var n = Normalize(needle);
        if (h.Length == 0 || n.Length == 0) return false;
        return $" {h} ".Contains($" {n} ", StringComparison.Ordinal);
    }

    public static bool SameText(string? a, string? b) =>
        Normalize(a).Length > 0 && Normalize(a) == Normalize(b);

    /// <summary>Stable fingerprint of an offer's content. Used as the cheap first layer of deduplication.</summary>
    public static string ContentHash(string? title, string? company, string? district, string? description)
    {
        var payload = string.Join('|', Normalize(title), Normalize(company), Normalize(district), Normalize(description));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
