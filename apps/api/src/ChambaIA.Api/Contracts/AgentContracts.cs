using System.ComponentModel.DataAnnotations;

namespace ChambaIA.Api.Contracts;

public sealed class AgentMessageRequest
{
    [Required, StringLength(500, MinimumLength = 2)]
    public string Text { get; init; } = "";
}

/// <summary>What the agent did with a message. `Changes` is empty when it did not understand or nothing changed.</summary>
public sealed record AgentReplyDto(string Reply, bool Understood, IReadOnlyList<string> Changes, MatchOverviewDto? Overview);
