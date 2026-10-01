using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Matching;

/// <summary>
/// Deterministic matching (stages A and B of the pipeline). Pure and free: no I/O, no AI, no tokens.
/// Semantic similarity (stage C, embeddings) is layered on top in phase 4 and only re-ranks what survives here.
/// </summary>
public static class MatchEngine
{
    // Weights sum to 1. Phase 4 will carve a semantic share out of the skills/role weights.
    private const double RulesWeight = 0.25;
    private const double SkillsWeight = 0.30;
    private const double RoleWeight = 0.25;
    private const double ExperienceWeight = 0.15;
    private const double EducationWeight = 0.05;

    private const double HardFilteredCap = 25;

    public static MatchOutcome Evaluate(CandidateProfile profile, JobPreferences prefs, JobOffer job)
    {
        var rules = RulesEvaluator.Evaluate(profile, prefs, job);
        var skills = SkillsEvaluator.Evaluate(profile, job);
        var role = RoleMatcher.Evaluate(profile, prefs, job);
        var experience = RulesEvaluator.ExperienceScore(profile, job);
        var education = RulesEvaluator.EducationScore(profile, job);

        var overall =
            RulesWeight * rules.Score +
            SkillsWeight * skills.Score +
            RoleWeight * role.Score +
            ExperienceWeight * experience +
            EducationWeight * education;

        var hardFiltered = rules.HardFailures.Count > 0;
        var category = hardFiltered
            ? MatchCategory.Poor
            : Cap(Categorize(overall), skills.Missing.Count, GapCount(skills, rules), role.Score);

        // Keep the score consistent with the (possibly capped) category so sorting by score never contradicts the label.
        overall = Math.Min(overall, hardFiltered ? HardFilteredCap : Ceiling(category));

        var reasons = new List<MatchNote>();
        if (role.BestRole is not null && role.Score >= 70)
            reasons.Add(new MatchNote("role-match", "Coincide con un cargo que buscas", role.BestRole));
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
