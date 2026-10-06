using System.ComponentModel.DataAnnotations;

namespace ChambaIA.Api.Contracts;

public sealed class AgentMessageRequest
{
    [Required, StringLength(500, MinimumLength = 2)]
    public string Text { get; init; } = "";
}

/// <summary>What the agent did with a message. `Changes` is empty when it did not understand or nothing changed.</summary>
public sealed record AgentReplyDto(string Reply, bool Understood, IReadOnlyList<string> Changes, MatchOverviewDto? Overview, AgentProposalDto? Proposal = null);

/// <summary>A command in the wire format. The same shape travels from the model's interpretation to the app and back when the person approves.</summary>
public sealed record AgentCommandDto(string Intent, string? Text, decimal? Number, string? Modality, bool? Flag);

/// <summary>
/// What the AI understood, not yet applied: the person sees the descriptions and decides. `Commands` are sent back unchanged to
/// POST /agent/apply (where they are validated again, like everything that did not come from our own rules).
/// </summary>
public sealed record AgentProposalDto(IReadOnlyList<AgentCommandDto> Commands, IReadOnlyList<string> Descriptions);

public sealed class AgentCommandRequest
{
    [Required, StringLength(40)]
    public string Intent { get; init; } = "";

    [StringLength(80)]
    public string? Text { get; init; }

    public decimal? Number { get; init; }

    [StringLength(20)]
    public string? Modality { get; init; }

    public bool? Flag { get; init; }
}

public sealed class ApplyAgentCommandsRequest
{
    [Required, MinLength(1), MaxLength(5)]
    public List<AgentCommandRequest> Commands { get; init; } = [];
}
