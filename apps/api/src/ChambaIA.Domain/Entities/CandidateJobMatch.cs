using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Entities;

public class CandidateJobMatch
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CandidateId { get; set; }
    public CandidateProfile? Candidate { get; set; }
    public Guid JobId { get; set; }
    public JobOffer? Job { get; set; }

    public double RulesScore { get; set; }
    public double SkillsScore { get; set; }
    /// <summary>Null until the embedding stage (phase 4) has run.</summary>
    public double? SemanticScore { get; set; }
    public double OverallScore { get; set; }
    public MatchCategory Category { get; set; }

    public List<string> MatchedSkills { get; set; } = [];
    public List<string> MissingSkills { get; set; } = [];
    public List<MatchNote> Reasons { get; set; } = [];
    public List<MatchNote> Warnings { get; set; } = [];

    public MatchStatus Status { get; set; } = MatchStatus.New;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A human-readable line of the explanation shown in "¿Por qué encaja conmigo?".</summary>
public class MatchNote
{
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Detail { get; set; }

    public MatchNote() { }

    public MatchNote(string code, string title, string? detail = null)
    {
        Code = code;
        Title = title;
        Detail = detail;
    }
}
