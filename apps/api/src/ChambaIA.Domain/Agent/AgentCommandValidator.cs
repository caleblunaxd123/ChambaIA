using System.Text.RegularExpressions;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Geo;
using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Agent;

/// <summary>A command as it travels over the wire (from a model, or back from the app when the user approves a proposal): all strings, nothing trusted.</summary>
public sealed record AgentCommandInput(string? Intent, string? Text = null, decimal? Number = null, string? Modality = null, bool? Flag = null);

/// <summary>
/// The only way a command that did not come from our own rules becomes an <see cref="AgentCommand"/>. It is a closed schema: known
/// intents only, numbers inside sane ranges, districts that exist, short plain-text phrases. Whatever does not fit is dropped, so
/// neither a model's mistake nor a tampered request can put an unexpected value in someone's preferences.
/// </summary>
public static partial class AgentCommandValidator
{
    public const int MaxCommands = 5;
    public const int MaxPhraseLength = 60;

    public static IReadOnlyList<AgentCommand> Validate(IEnumerable<AgentCommandInput>? inputs)
    {
        if (inputs is null) return [];

        var result = new List<AgentCommand>();
        foreach (var input in inputs)
        {
            if (result.Count >= MaxCommands) break;
            if (!Enum.TryParse<AgentIntent>(input.Intent?.Trim(), ignoreCase: true, out var intent) || !Enum.IsDefined(intent)) continue;
            if (Build(intent, input) is { } command && !result.Contains(command)) result.Add(command);
        }
        return result;
    }

    private static AgentCommand? Build(AgentIntent intent, AgentCommandInput input) => intent switch
    {
        AgentIntent.SetMinSalary => input.Number is >= 500 and <= 50_000 ? new(intent, Number: decimal.Round(input.Number.Value, 0)) : null,
        AgentIntent.SetMaxCommute => input.Number is >= 5 and <= 240 ? new(intent, Number: decimal.Round(input.Number.Value, 0)) : null,
        AgentIntent.SetWeekdaysOnly => input.Flag is { } flag ? new(intent, Flag: flag) : null,
        AgentIntent.SetModality or AgentIntent.AddModality or AgentIntent.RemoveModality =>
            ParseModality(input.Modality) is { } modality ? new(intent, Modality: modality) : null,
        AgentIntent.AddDistrict or AgentIntent.ExcludeDistrict or AgentIntent.AllowDistrict or AgentIntent.SetHomeDistrict =>
            LimaDistricts.Find(input.Text) is { } district ? new(intent, Text: district.Name) : null,
        AgentIntent.AddRole or AgentIntent.RemoveRole or AgentIntent.ExcludeKeyword or AgentIntent.AllowKeyword =>
            CleanPhrase(input.Text) is { } phrase ? new(intent, Text: phrase) : null,
        _ => null
    };

    private static WorkModality? ParseModality(string? raw) => TextNormalizer.Normalize(raw) switch
    {
        "onsite" or "on site" or "presencial" or "presenciales" => WorkModality.OnSite,
        "hybrid" or "hibrido" or "hibridos" or "mixto" => WorkModality.Hybrid,
        "remote" or "remoto" or "remotos" or "teletrabajo" => WorkModality.Remote,
        _ => null
    };

    /// <summary>A role or keyword: a short plain phrase. Links, addresses, markup and anything that is not a word are rejected.</summary>
    public static string? CleanPhrase(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var phrase = Spaces().Replace(raw.Trim(), " ");
        if (phrase.Length is < 2 or > MaxPhraseLength) return null;
        return Allowed().IsMatch(phrase) ? phrase : null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    // Letters (with accents), digits, spaces and a few joiners. No @, :, /, quotes, brackets or other symbols.
    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} .+&/-]*$")]
    private static partial Regex Allowed();
}
