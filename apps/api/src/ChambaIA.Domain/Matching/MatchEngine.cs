using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Matching;

/// <summary>
/// Deterministic matching (stages A and B of the pipeline). Pure and free: no I/O, no AI, no tokens.
/// Semantic similarity (stage C, embeddings) is optional: when a score is supplied it takes a share of the weights and can
/// soften the role-title cap; when it is not (no embeddings yet, provider down) the result is exactly the deterministic one.
/// </summary>
public static class MatchEngine
{
    // Each set of weights sums to 1. With a semantic score, it takes 25% (mostly from title/role and skills).
    private static readonly Weights Plain = new(Rules: 0.25, Skills: 0.30, Role: 0.25, Experience: 0.15, Education: 0.05, Semantic: 0);
    private static readonly Weights WithSemantic = new(Rules: 0.20, Skills: 0.25, Role: 0.15, Experience: 0.10, Education: 0.05, Semantic: 0.25);

    /// <summary>A strong semantic match above this score is a reason worth telling the user about.</summary>
    private const double SemanticReasonThreshold = 70;

    private sealed record Weights(double Rules, double Skills, double Role, double Experience, double Education, double Semantic);

    private const double HardFilteredCap = 25;

    /// <param name="semanticScore">0-100 from <see cref="SemanticSimilarity"/>, or null when embeddings are not available.</param>
    public static MatchOutcome Evaluate(CandidateProfile profile, JobPreferences prefs, JobOffer job, double? semanticScore = null)
    {
        var rules = RulesEvaluator.Evaluate(profile, prefs, job);
        var skills = SkillsEvaluator.Evaluate(profile, job);
        var role = RoleMatcher.Evaluate(profile, prefs, job);
        var experience = RulesEvaluator.ExperienceScore(profile, job);
        var education = RulesEvaluator.EducationScore(profile, job);

        var w = semanticScore is null ? Plain : WithSemantic;
        var overall =
            w.Rules * rules.Score +
            w.Skills * skills.Score +
            w.Role * role.Score +
            w.Experience * experience +
            w.Education * education +
            w.Semantic * (semanticScore ?? 0);

        // A title that reads differently but means the same ("Auxiliar de oficina" vs "Asistente administrativo") should not be
        // punished as an unrelated role: the semantic score stands in for the literal title overlap, discounted.
        var effectiveRole = semanticScore is { } sem ? Math.Max(role.Score, sem * 0.85) : role.Score;

        var hardFiltered = rules.HardFailures.Count > 0;
        var category = hardFiltered
            ? MatchCategory.Poor
            : Cap(Categorize(overall), skills.Missing.Count, GapCount(skills, rules), effectiveRole);

        // Keep the score consistent with the (possibly capped) category so sorting by score never contradicts the label.
        overall = Math.Min(overall, hardFiltered ? HardFilteredCap : Ceiling(category));

        var reasons = new List<MatchNote>();
        if (role.BestRole is not null && role.Score >= 70)
            reasons.Add(new MatchNote("role-match", "Coincide con un cargo que buscas", role.BestRole));
        if (semanticScore is >= SemanticReasonThreshold && role.Score < 70)
            reasons.Add(new MatchNote("semantic-match", "El contenido de la oferta se parece mucho a tu experiencia"));
        reasons.AddRange(skills.Reasons);
        reasons.AddRange(rules.Reasons);

        // Blocking problems first, then softer ones, so the UI can show the most important risks on top.
        var warnings = new List<MatchNote>();
        warnings.AddRange(rules.HardFailures);
        warnings.AddRange(skills.Warnings);
        warnings.AddRange(rules.Warnings);

        return new MatchOutcome
        {
            RulesScore = Round(rules.Score),
            SkillsScore = Round(skills.Score),
            RoleScore = Round(role.Score),
            ExperienceScore = Round(experience),
            SemanticScore = semanticScore is { } sc ? Round(sc) : null,
            OverallScore = Round(overall),
            Category = category,
            IsHardFiltered = hardFiltered,
            MatchedSkills = skills.Matched,
            MissingSkills = skills.Missing,
            Reasons = reasons,
            Warnings = warnings
        };
    }

    public static MatchCategory Categorize(double overall) => overall switch
    {
        >= 80 => MatchCategory.Excellent,
        >= 65 => MatchCategory.VeryCompatible,
        >= 50 => MatchCategory.Compatible,
        >= 35 => MatchCategory.Review,
        _ => MatchCategory.Poor
    };

    private static double Ceiling(MatchCategory category) => category switch
    {
        MatchCategory.Excellent => 100,
        MatchCategory.VeryCompatible => 79.9,
        MatchCategory.Compatible => 64.9,
        MatchCategory.Review => 49.9,
        _ => 34.9
    };

    private static readonly HashSet<string> GapCodes = ["skill-level-gap", "experience-close", "education-gap"];

    private static int GapCount(SkillsEvaluator.Result skills, StageResult rules) =>
        skills.Warnings.Concat(rules.Warnings).Count(w => GapCodes.Contains(w.Code));

    /// <summary>
    /// A weighted average hides deal-breakers, so the score alone never decides: unmet requirements cap the category
    /// ("Excelente" means nothing is missing; one missing requirement can never be better than "Muy compatible").
    /// </summary>
    internal static MatchCategory Cap(MatchCategory byScore, int missing, int gaps, double roleScore)
    {
        var cap = (missing, gaps) switch
        {
            (0, 0) => MatchCategory.Excellent,
            (0, <= 2) => MatchCategory.VeryCompatible,
            (1, 0) => MatchCategory.VeryCompatible,
            _ when missing + gaps <= 2 => MatchCategory.Compatible,
            _ => MatchCategory.Review
        };

        // A title that barely resembles the roles the candidate targets is never a top recommendation.
        if (roleScore < 30) cap = (MatchCategory)Math.Min((int)cap, (int)MatchCategory.Compatible);
        else if (roleScore < 60) cap = (MatchCategory)Math.Min((int)cap, (int)MatchCategory.VeryCompatible);

        return (MatchCategory)Math.Min((int)byScore, (int)cap);
    }

    private static double Round(double value) => Math.Round(value, 1);
}
