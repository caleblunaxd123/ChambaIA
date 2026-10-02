using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Matching;

public enum DimensionLevel { Weak, Medium, Strong }

/// <summary>One axis of "¿por qué encaja conmigo?". Qualitative on purpose: a level and a sentence, never a percentage.</summary>
public sealed record MatchDimension(string Key, string Label, DimensionLevel Level, string Note);

/// <summary>A concrete, honest "what would change" suggestion: only about things the candidate may truly have.</summary>
public sealed record MatchImprovement(string Title, string Detail, MatchCategory ResultCategory);

public sealed record MatchExplanation(IReadOnlyList<MatchDimension> Dimensions, IReadOnlyList<MatchImprovement> Improvements);

/// <summary>
/// Builds the rich explanation of one match from the same engine that produced it, so what the user reads can never disagree
/// with the category they see. Pure and deterministic; the improvements are what-if runs of the engine, not text generation.
/// </summary>
public static class MatchExplainer
{
    private const double StrongFrom = 75;
    private const double MediumFrom = 45;
    private const int MaxImprovements = 3;

    public static MatchExplanation Explain(CandidateProfile profile, JobPreferences prefs, JobOffer job, double? semanticScore)
    {
        var outcome = MatchEngine.Evaluate(profile, prefs, job, semanticScore);
        return new MatchExplanation(Dimensions(profile, job, outcome), Improvements(profile, prefs, job, semanticScore, outcome));
    }

    // ---------------------------------------------------------------------------------------------- dimensions

    private static List<MatchDimension> Dimensions(CandidateProfile profile, JobOffer job, MatchOutcome outcome)
    {
        var dims = new List<MatchDimension>
        {
            new("role", "Cargo", Level(outcome.RoleScore), RoleNote(outcome)),
            new("skills", "Habilidades", Level(outcome.SkillsScore), SkillsNote(job, outcome)),
            new("experience", "Experiencia", Level(outcome.ExperienceScore), ExperienceNote(profile, job)),
            new("conditions", "Condiciones", outcome.IsHardFiltered ? DimensionLevel.Weak : Level(outcome.RulesScore), ConditionsNote(outcome))
        };

        if (outcome.SemanticScore is { } semantic)
            dims.Add(new MatchDimension("meaning", "Parecido con tu perfil", Level(semantic), MeaningNote(semantic)));

        return dims;
    }

    private static DimensionLevel Level(double score) => score switch
    {
        >= StrongFrom => DimensionLevel.Strong,
        >= MediumFrom => DimensionLevel.Medium,
        _ => DimensionLevel.Weak
    };

    private static string RoleNote(MatchOutcome o)
    {
        if (o.Reasons.FirstOrDefault(r => r.Code == "role-match") is { Detail: { } role }) return $"Coincide con «{role}», un cargo que buscas.";
        if (o.Reasons.Any(r => r.Code == "semantic-match")) return "El título es distinto, pero el contenido se parece a tu experiencia.";
        return o.RoleScore >= MediumFrom ? "El título se parece en parte a los cargos que buscas." : "El título no se parece a los cargos que buscas.";
    }

    private static string SkillsNote(JobOffer job, MatchOutcome o)
    {
        var required = job.SkillsRequired.Count;
        if (required == 0) return "No piden habilidades específicas.";

        var have = required - o.MissingSkills.Count;
        var gaps = o.Warnings.Count(w => w.Code == "skill-level-gap");
        var note = $"Tienes {have} de {required} de las que piden.";
        return gaps > 0 ? $"{note} {(gaps == 1 ? "En 1 piden un nivel mayor" : $"En {gaps} piden un nivel mayor")} al tuyo." : note;
    }

    private static string ExperienceNote(CandidateProfile profile, JobOffer job) => job.ExperienceRequiredMinMonths switch
    {
        null => "No indican experiencia mínima.",
        0 => "No exigen experiencia previa.",
        { } months => $"Piden {MatchFormatting.Duration(months)}; tienes {MatchFormatting.Duration(profile.ExperienceMonths)}."
    };

    private static string ConditionsNote(MatchOutcome o)
    {
        if (o.Warnings.FirstOrDefault(w => !w.Code.StartsWith("skill-", StringComparison.Ordinal)) is { } issue)
            return o.IsHardFiltered ? $"No cumple lo que pediste: {issue.Title.ToLowerInvariant()}." : $"{issue.Title}.";
        return "Cumple lo que pediste (sueldo, ubicación y horario).";
    }

    private static string MeaningNote(double semantic) => Level(semantic) switch
    {
        DimensionLevel.Strong => "El contenido de la oferta se parece mucho a lo que has hecho.",
        DimensionLevel.Medium => "Parte del contenido se parece a tu experiencia.",
        _ => "El contenido de la oferta es distinto a lo que has hecho."
    };

    // -------------------------------------------------------------------------------------------- improvements

    private static List<MatchImprovement> Improvements(CandidateProfile profile, JobPreferences prefs, JobOffer job, double? semantic, MatchOutcome current)
    {
        // Hard filters are about what the candidate asked to avoid, not about skills: adding skills cannot (and should not) fix them.
        if (current.IsHardFiltered || current.Category == MatchCategory.Excellent) return [];

        var have = profile.Skills.GroupBy(s => s.Key).ToDictionary(g => g.Key, g => g.Max(s => s.Level));
        var unmet = job.SkillsRequired
            .Where(r => !have.TryGetValue(r.Key, out var level) || (r.MinLevel is { } min && level < min))
            .ToList();
        if (unmet.Count == 0) return [];

        var results = new List<(MatchImprovement Item, int Changes)>();

        foreach (var req in unmet)
        {
            var after = Category(profile, prefs, job, semantic, [req]);
            if (after <= current.Category) continue;

            var name = string.IsNullOrWhiteSpace(req.Name) ? req.Key : req.Name;
            var level = req.MinLevel ?? SkillLevel.Intermediate;
            var title = have.ContainsKey(req.Key)
                ? $"Sube «{name}» a nivel {MatchFormatting.Level(level)} en tu perfil"
                : $"Agrega «{name}» a tu perfil";
            results.Add((new MatchImprovement(title, Detail(current.Category, after), after), 1));
        }

        if (unmet.Count >= 2)
        {
            var all = Category(profile, prefs, job, semantic, unmet);
            var bestSingle = results.Count == 0 ? current.Category : results.Max(r => r.Item.ResultCategory);
            if (all > bestSingle)
                results.Add((new MatchImprovement($"Completa las {unmet.Count} habilidades que te faltan", Detail(current.Category, all), all), unmet.Count));
        }

        return results
            .OrderByDescending(r => r.Item.ResultCategory)
            .ThenBy(r => r.Changes)
            .Take(MaxImprovements)
            .Select(r => r.Item)
            .ToList();
    }

    private static string Detail(MatchCategory from, MatchCategory to) =>
        $"Solo si ya lo sabes hacer. Esta oferta pasaría de «{MatchFormatting.Category(from)}» a «{MatchFormatting.Category(to)}».";

    private static MatchCategory Category(CandidateProfile profile, JobPreferences prefs, JobOffer job, double? semantic, IEnumerable<SkillRequirement> adding)
    {
        var skills = profile.Skills.ToDictionary(s => s.Key, s => s);
        foreach (var req in adding)
            skills[req.Key] = new ProfileSkill { Key = req.Key, Name = req.Name, Level = req.MinLevel ?? SkillLevel.Intermediate };

        // A what-if copy: only the fields the engine reads, so the real profile is never touched.
        var hypothetical = new CandidateProfile
        {
            UserId = profile.UserId,
            Headline = profile.Headline,
            ExperienceMonths = profile.ExperienceMonths,
            EducationLevel = profile.EducationLevel,
            EducationStatus = profile.EducationStatus,
            Skills = [.. skills.Values],
            Experience = profile.Experience,
            Education = profile.Education
        };
        return MatchEngine.Evaluate(hypothetical, prefs, job, semantic).Category;
    }
}
