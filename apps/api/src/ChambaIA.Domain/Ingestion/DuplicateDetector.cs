using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Ingestion;

/// <summary>What the similarity layer compares. Cheap to build from a stored offer or a freshly normalised one.</summary>
public sealed record OfferFingerprint(
    string NormalizedCompany,
    string Title,
    string? District,
    decimal? SalaryMin,
    decimal? SalaryMax,
    DateTimeOffset? PostedAt);

/// <summary>
/// Third (and last) layer of deduplication, after the exact ones: <c>(source, externalId)</c> and the content hash.
/// The same company republishing the same role on another portal, with slightly different wording, is one offer
/// for the user. The rules are deliberately strict: showing a duplicate is a nuisance, hiding a different job is a loss.
/// </summary>
public static class DuplicateDetector
{
    public const double TitleThreshold = 0.75;
    public static readonly TimeSpan Window = TimeSpan.FromDays(21);

    public static bool LooksLikeSameOffer(OfferFingerprint a, OfferFingerprint b)
    {
        if (a.NormalizedCompany.Length == 0 || a.NormalizedCompany != b.NormalizedCompany) return false;
        if (a.District is not null && b.District is not null && !string.Equals(a.District, b.District, StringComparison.OrdinalIgnoreCase)) return false;
        if (a.PostedAt is { } pa && b.PostedAt is { } pb && (pa - pb).Duration() > Window) return false;
        if (!SalariesCompatible(a, b)) return false;
        return TitleSimilarity(a.Title, b.Title) >= TitleThreshold;
    }

    /// <summary>Jaccard similarity of the title stems ("Auxiliar administrativo" ≈ "Asistente Administrativa").</summary>
    public static double TitleSimilarity(string a, string b)
    {
        var sa = TextNormalizer.Stems(a).ToHashSet(StringComparer.Ordinal);
        var sb = TextNormalizer.Stems(b).ToHashSet(StringComparer.Ordinal);
        if (sa.Count == 0 || sb.Count == 0) return 0;
        var intersection = sa.Count(sb.Contains);
        return (double)intersection / (sa.Count + sb.Count - intersection);
    }

    /// <summary>Two offers that both state a salary must have overlapping ranges to be the same offer.</summary>
    private static bool SalariesCompatible(OfferFingerprint a, OfferFingerprint b)
    {
        if ((a.SalaryMin ?? a.SalaryMax) is null || (b.SalaryMin ?? b.SalaryMax) is null) return true;
        var (aLo, aHi) = (a.SalaryMin ?? a.SalaryMax!.Value, a.SalaryMax ?? a.SalaryMin!.Value);
        var (bLo, bHi) = (b.SalaryMin ?? b.SalaryMax!.Value, b.SalaryMax ?? b.SalaryMin!.Value);
        return aLo <= bHi && bLo <= aHi;
    }
}
