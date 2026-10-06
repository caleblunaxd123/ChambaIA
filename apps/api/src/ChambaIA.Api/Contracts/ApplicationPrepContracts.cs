using System.ComponentModel.DataAnnotations;

namespace ChambaIA.Api.Contracts;

public sealed record AiQuotaDto(int CallsToday, int DailyLimit, int RemainingToday);

/// <summary>What would be sent to the model, exactly, and what never is. The person reads this before any call is made.</summary>
public sealed record ApplicationPrepPreviewDto(
    bool Available,
    string? UnavailableReason,
    string Payload,
    IReadOnlyList<string> Includes,
    IReadOnlyList<string> Excludes,
    AiQuotaDto Quota);

public sealed class ApplicationPrepRequest
{
    /// <summary>Must be true: the explicit approval of sending the previewed data to an AI model.</summary>
    public bool Approved { get; init; }

    [RegularExpression("^(formal|cercano)$", ErrorMessage = "El tono debe ser «formal» o «cercano».")]
    public string Tone { get; init; } = "formal";
}

public sealed record ApplicationPrepDraftDto(
    string Message,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> Warnings);
