using System.Security.Cryptography;
using System.Text;
using ChambaIA.Domain.Entities;

namespace ChambaIA.Domain.Matching;

/// <summary>
/// The exact text that gets embedded for a job or a candidate. Short on purpose (cost and latency scale with length) and
/// stable: the hash of this text decides whether a stored embedding is still valid, so unchanged content is never re-embedded.
/// </summary>
public static class EmbeddingText
{
    public const int MaxChars = 1800;

    public static string ForJob(JobOffer job)
    {
        var sb = new StringBuilder();
        sb.Append(job.Title).Append(". ").Append(job.Company);
        if (!string.IsNullOrWhiteSpace(job.Industry)) sb.Append(". Sector: ").Append(job.Industry);
        if (job.SkillsRequired.Count > 0) sb.Append(". Requisitos: ").Append(string.Join(", ", job.SkillsRequired.Select(s => s.Name)));
        if (job.SkillsPreferred.Count > 0) sb.Append(". Valoran: ").Append(string.Join(", ", job.SkillsPreferred.Select(s => s.Name)));
        sb.Append(". ").Append(job.Description);
        return Clip(sb.ToString());
    }

    /// <summary>What the candidate has done and wants: headline, target roles, skills and their work history. No names, no contact data.</summary>
    public static string ForProfile(CandidateProfile profile, JobPreferences prefs)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(profile.Headline)) sb.Append(profile.Headline).Append(". ");
        if (prefs.PreferredRoles.Count > 0) sb.Append("Busco trabajo como: ").Append(string.Join(", ", prefs.PreferredRoles)).Append(". ");
        if (profile.Skills.Count > 0) sb.Append("Habilidades: ").Append(string.Join(", ", profile.Skills.Select(s => s.Name))).Append(". ");
        foreach (var e in profile.Experience.Take(6))
        {
            sb.Append("Experiencia: ").Append(e.Title);
            if (!string.IsNullOrWhiteSpace(e.Description)) sb.Append(" - ").Append(e.Description);
            sb.Append(". ");
        }
        return Clip(sb.ToString().Trim());
    }

    /// <summary>True when there is enough substance to embed (an empty profile would match everything and nothing).</summary>
    public static bool IsMeaningful(string text) => text.Count(char.IsLetter) >= 20;

    public static string Hash(string text, string model) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(model + "\n" + text))).ToLowerInvariant();

    private static string Clip(string text) => text.Length <= MaxChars ? text : text[..MaxChars];
}
