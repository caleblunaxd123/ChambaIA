namespace ChambaIA.Domain.Matching;

/// <summary>
/// Turns the cosine similarity of two embeddings into the 0-100 "semantic score" of the matching engine.
/// Raw cosine values are not comparable across models: with bge-m3, unrelated Spanish texts still sit around 0.45-0.5 and
/// close ones around 0.65-0.8, so the useful range is stretched. The anchors below were measured with bge-m3 on Spanish job
/// texts (same role 0.79, related role 0.64, unrelated trade 0.48) and are options of the model: re-measure them (see
/// docs/OLLAMA.md) whenever the model changes.
/// </summary>
public static class SemanticSimilarity
{
    /// <summary>Cosine at or below which the texts are considered unrelated (score 0).</summary>
    public const double UnrelatedCosine = 0.45;

    /// <summary>Cosine at or above which the texts are considered the same kind of job (score 100).</summary>
    public const double IdenticalKindCosine = 0.78;

    public static double ToScore(double cosine, double low = UnrelatedCosine, double high = IdenticalKindCosine)
    {
        if (double.IsNaN(cosine)) return 0;
        return Math.Round(Math.Clamp((cosine - low) / (high - low), 0, 1) * 100, 1);
    }

    public static double Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length || a.Length == 0) return double.NaN;

        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * (double)b[i];
            na += a[i] * (double)a[i];
            nb += b[i] * (double)b[i];
        }
        return na == 0 || nb == 0 ? double.NaN : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}
