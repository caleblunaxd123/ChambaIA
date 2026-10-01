using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Skills;
using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Matching;

/// <summary>Stage B (skills part): compares canonical skill keys and levels.</summary>
internal static class SkillsEvaluator
{
    public sealed record Result(double Score, List<string> Matched, List<string> Missing, List<MatchNote> Reasons, List<MatchNote> Warnings);

    public static Result Evaluate(CandidateProfile profile, JobOffer job)
    {
        var have = profile.Skills
            .GroupBy(s => s.Key)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Level).First());

        var matched = new List<string>();
        var missing = new List<string>();
        var reasons = new List<MatchNote>();
        var warnings = new List<MatchNote>();
        double points = 0;

        foreach (var req in job.SkillsRequired)
        {
            var name = NameOf(req);
            if (!have.TryGetValue(req.Key, out var skill))
            {
                missing.Add(name);
                warnings.Add(new MatchNote("skill-missing", $"Piden {name}", "No aparece en tu perfil."));
            }
            else if (req.MinLevel is { } min && skill.Level < min)
            {
                points += 0.5;
                matched.Add(name);
                warnings.Add(new MatchNote("skill-level-gap", $"Solicitan {name} {MatchFormatting.Level(min)}",
                    $"Tu perfil indica nivel {MatchFormatting.Level(skill.Level)}."));
            }
            else
            {
                points += 1;
                matched.Add(name);
                reasons.Add(new MatchNote("skill-match", $"Piden {name}", "Está en tu perfil."));
            }
        }

        var baseScore = job.SkillsRequired.Count == 0 ? 60 : 100.0 * points / job.SkillsRequired.Count;

        var preferredHits = job.SkillsPreferred.Where(p => have.ContainsKey(p.Key)).Select(NameOf).ToList();
        if (preferredHits.Count > 0)
            reasons.Add(new MatchNote("preferred-skill", "Además valoran " + string.Join(", ", preferredHits), "También las tienes."));

        var score = Math.Min(100, baseScore + Math.Min(10, preferredHits.Count * 5));
        return new Result(score, matched, missing, reasons, warnings);
    }

    private static string NameOf(SkillRequirement req) =>
        !string.IsNullOrWhiteSpace(req.Name) ? req.Name : SkillCatalog.FindByKey(req.Key)?.Name ?? req.Key;
}

/// <summary>Stage B (role part): how close the offer title is to the roles the candidate targets.</summary>
internal static class RoleMatcher
{
    public sealed record Result(double Score, string? BestRole);

    public static Result Evaluate(CandidateProfile profile, JobPreferences prefs, JobOffer job)
    {
        var roles = prefs.PreferredRoles.Count > 0
            ? prefs.PreferredRoles
            : string.IsNullOrWhiteSpace(profile.Headline) ? [] : new List<string> { profile.Headline };
        if (roles.Count == 0) return new Result(50, null);

        var titleTokens = TextNormalizer.Stems(job.Title).ToHashSet();
        double best = 0;
        string? bestRole = null;

        foreach (var role in roles)
        {
            var tokens = TextNormalizer.Stems(role);
            if (tokens.Count == 0) continue;
            var score = 100.0 * tokens.Count(t => titleTokens.Any(title => SameWord(t, title))) / tokens.Count;
            if (score > best)
            {
                best = score;
                bestRole = role;
            }
        }

        return new Result(best, bestRole);
    }

    /// <summary>Equal stems, or long words sharing a 7+ letter prefix ("facturador" ~ "facturacion").</summary>
    private static bool SameWord(string a, string b)
    {
        if (a == b) return true;
        if (a.Length < 8 || b.Length < 8) return false;
        return a.AsSpan(0, 7).SequenceEqual(b.AsSpan(0, 7));
    }
}
