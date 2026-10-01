using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Matching;

/// <summary>Result of evaluating one job for one candidate. Pure data: persisted by the caller.</summary>
public sealed class MatchOutcome
{
    public double RulesScore { get; init; }
    public double SkillsScore { get; init; }
    public double RoleScore { get; init; }
    public double ExperienceScore { get; init; }
    public double OverallScore { get; init; }
    public MatchCategory Category { get; init; }
    /// <summary>True when a hard rule failed: the offer conflicts with something the candidate asked to avoid.</summary>
    public bool IsHardFiltered { get; init; }
    public List<string> MatchedSkills { get; init; } = [];
    public List<string> MissingSkills { get; init; } = [];
    public List<MatchNote> Reasons { get; init; } = [];
    public List<MatchNote> Warnings { get; init; } = [];
}

/// <summary>Partial result of one evaluation stage, merged by <see cref="MatchEngine"/>.</summary>
internal sealed class StageResult
{
    public double Score { get; set; } = 100;
    public List<MatchNote> Reasons { get; } = [];
    public List<MatchNote> Warnings { get; } = [];
    public List<MatchNote> HardFailures { get; } = [];
}
