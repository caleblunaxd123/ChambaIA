using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Agent;

public enum AgentIntent
{
    SetMinSalary,
    AddRole,
    RemoveRole,
    AddDistrict,
    ExcludeDistrict,
    AllowDistrict,
    SetHomeDistrict,
    SetModality,
    AddModality,
    RemoveModality,
    SetMaxCommute,
    SetWeekdaysOnly,
    ExcludeKeyword,
    AllowKeyword
}

/// <summary>A structured instruction extracted from free text ("No me muestres trabajos en Ate" → ExcludeDistrict("Ate")).</summary>
public sealed record AgentCommand(AgentIntent Intent, string? Text = null, decimal? Number = null, WorkModality? Modality = null, bool? Flag = null);
