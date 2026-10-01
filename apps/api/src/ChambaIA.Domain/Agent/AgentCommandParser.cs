using System.Text.RegularExpressions;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Geo;
using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Agent;

/// <summary>
/// Rule-based command extraction: free Spanish text → structured preference changes. Zero cost, zero latency.
/// Phase 9 adds a small-LLM fallback for the sentences these rules do not understand; the rules always run first.
/// </summary>
public static partial class AgentCommandParser
{
    private static readonly string[] NegativeCues =
    [
        "no me muestres", "no me muestre", "no me muestren", "no me ofrezcas", "no me mandes", "no me envies", "no quiero", "no me interesa",
        "no me interesan", "no busques", "ya no quiero", "evita", "evitame", "descarta", "descartame", "sin", "nada de", "no aceptes"
    ];

    private static readonly string[] AllowCues =
    [
        "vuelve a mostrar", "vuelve a mostrarme", "ya puedes mostrar", "ya puedes mostrarme", "ya puedes ofrecerme", "ahora si quiero", "ahora si acepto", "permite", "permitir"
    ];

    private static readonly string[] RoleAddCues =
    [
        "busca tambien", "buscame tambien", "agrega", "agregame", "anade", "anademe", "incluye", "incluyeme", "tambien busca", "tambien quiero", "tambien me interesa", "tambien me interesan"
    ];

    private static readonly string[] RoleRemoveCues = ["quita", "quitame", "elimina", "eliminame", "ya no busques", "deja de buscar", "borra"];

    private static readonly string[] Fillers =
    [
        "trabajos", "trabajo", "empleos", "empleo", "ofertas", "oferta", "chambas", "chamba", "puestos", "puesto", "vacantes", "vacante", "de", "en", "del", "los", "las", "el", "la", "un", "una", "que", "sean", "me", "tipo", "relacionados", "relacionadas", "con"
    ];

    public static IReadOnlyList<AgentCommand> Parse(string? input)
    {
        var text = TextNormalizer.Normalize(input);
        if (text.Length == 0) return [];

        var commands = new List<AgentCommand>();

        foreach (var clause in Split(text))
        {
            ParseSalary(clause, commands);
            ParseCommute(clause, commands);
            ParseSchedule(clause, commands);
            ParseModality(clause, commands);
            ParseHome(clause, commands);
            ParseListEdits(clause, commands);
        }

        return commands.Distinct().ToList();
    }

    // "no quiero trabajos en Ate, ni call center" → separate clauses. "y" only splits before a new cue.
    private static IEnumerable<string> Split(string text) =>
        SplitRegex().Split(text).Select(c => c.Trim()).Where(c => c.Length > 0);

    [GeneratedRegex(@"\s*(?:,| pero | ni | ademas | tambien no | y no | y ya no | y evita | y sin )\s*|\s+\.\s+")]
    private static partial Regex SplitRegex();

    // ---- salary -------------------------------------------------------------------------------------------------

    [GeneratedRegex(@"(?:minimo|al menos|no menos de|desde|gano|sueldo de|sueldo minimo|pretension)\s+(?:de\s+)?(?:s\s+)?(\d{1,2}[ ,.]?\d{3}|\d{3,5})(?:\s*(?:soles|s))?")]
    private static partial Regex SalaryBefore();

    [GeneratedRegex(@"(\d{1,2}[ ,.]?\d{3}|\d{3,5})\s*(?:soles|s)?\s*(?:de\s+)?(?:minimo|a mas|para arriba)")]
    private static partial Regex SalaryAfter();

    private static void ParseSalary(string clause, List<AgentCommand> into)
    {
        var match = SalaryBefore().Match(clause);
        if (!match.Success) match = SalaryAfter().Match(clause);
        if (!match.Success) return;

        var digits = new string(match.Groups[1].Value.Where(char.IsDigit).ToArray());
        if (decimal.TryParse(digits, out var amount) && amount is >= 500 and <= 50_000)
            into.Add(new AgentCommand(AgentIntent.SetMinSalary, Number: amount));
    }

    // ---- commute ------------------------------------------------------------------------------------------------

    [GeneratedRegex(@"(?:maximo|hasta|no mas de|menos de|como maximo|a lo mucho)\s+(?:de\s+)?(?:(media hora)|(una hora y media|hora y media)|(una hora|1 hora|1h)|(dos horas|2 horas|2h)|(\d{1,3})\s*(?:min|minutos)|(\d{1,2})\s*(?:horas|hora|h))\b")]
    private static partial Regex Commute();

    private static void ParseCommute(string clause, List<AgentCommand> into)
    {
        if (!clause.Contains("viaj") && !clause.Contains("trayecto") && !clause.Contains("camino") && !clause.Contains("lejos") && !clause.Contains("transporte")) return;

        var m = Commute().Match(clause);
        if (!m.Success) return;

        int? minutes = true switch
        {
            _ when m.Groups[1].Success => 30,
            _ when m.Groups[2].Success => 90,
            _ when m.Groups[3].Success => 60,
            _ when m.Groups[4].Success => 120,
            _ when m.Groups[5].Success => int.Parse(m.Groups[5].Value),
            _ when m.Groups[6].Success => int.Parse(m.Groups[6].Value) * 60,
            _ => null
        };

        if (minutes is >= 5 and <= 240)
            into.Add(new AgentCommand(AgentIntent.SetMaxCommute, Number: minutes));
    }

    // ---- schedule -----------------------------------------------------------------------------------------------

    private static void ParseSchedule(string clause, List<AgentCommand> into)
    {
        var weekdays = clause.Contains("lunes a viernes") || clause.Contains("sin sabados") || clause.Contains("sin domingos") || clause.Contains("sin fines de semana")
            || clause.Contains("no sabados") || clause.Contains("no trabajar sabados") || clause.Contains("no fines de semana") || clause.Contains("no quiero sabados");
        var weekends = clause.Contains("tambien sabados") || clause.Contains("acepto sabados") || clause.Contains("sabados si") || clause.Contains("fines de semana si");

        if (weekends) into.Add(new AgentCommand(AgentIntent.SetWeekdaysOnly, Flag: false));
        else if (weekdays) into.Add(new AgentCommand(AgentIntent.SetWeekdaysOnly, Flag: true));
    }

    // ---- modality -----------------------------------------------------------------------------------------------

    [GeneratedRegex(@"\b(remotos?|remotas?|presenciales|presencial|hibridos?|hibridas?|desde casa|home office)\b")]
    private static partial Regex ModalityWord();

    private static WorkModality ToModality(string word) => word switch
    {
        _ when word.StartsWith("presencial") => WorkModality.OnSite,
        _ when word.StartsWith("hibrid") => WorkModality.Hybrid,
        _ => WorkModality.Remote
    };

    private static void ParseModality(string clause, List<AgentCommand> into)
    {
        var m = ModalityWord().Match(clause);
        if (!m.Success) return;
        var modality = ToModality(m.Value);

        if (StartsWithAny(clause, NegativeCues))
            into.Add(new AgentCommand(AgentIntent.RemoveModality, Modality: modality));
        else if (clause.Contains("tambien") || clause.Contains("acepto"))
            into.Add(new AgentCommand(AgentIntent.AddModality, Modality: modality));
        else if (clause.Contains("solo") || clause.Contains("unicamente") || clause.Contains("prefiero") || clause.Contains("quiero"))
            into.Add(new AgentCommand(AgentIntent.SetModality, Modality: modality));
    }

    // ---- home district ------------------------------------------------------------------------------------------

    [GeneratedRegex(@"(?:vivo en|vivo cerca de|vivo por|mi casa esta en|mi casa queda en|estoy en|resido en)\s+(?<d>[a-z ]+)")]
    private static partial Regex Home();

    private static void ParseHome(string clause, List<AgentCommand> into)
    {
        var m = Home().Match(clause);
        if (!m.Success) return;
        if (FindDistrict(m.Groups["d"].Value) is { } district)
            into.Add(new AgentCommand(AgentIntent.SetHomeDistrict, Text: district.Name));
    }

    // ---- districts, roles and keywords --------------------------------------------------------------------------

    private static void ParseListEdits(string clause, List<AgentCommand> into)
    {
        if (into.Any(c => c.Intent == AgentIntent.SetHomeDistrict) && Home().IsMatch(clause)) return;

        var negative = StartsWithAny(clause, NegativeCues);
        var allow = StartsWithAny(clause, AllowCues);
        var removeRole = StartsWithAny(clause, RoleRemoveCues);
        var addRole = StartsWithAny(clause, RoleAddCues);

        var districts = FindDistricts(clause);

        if (districts.Count > 0)
        {
            foreach (var d in districts)
            {
                if (negative) into.Add(new AgentCommand(AgentIntent.ExcludeDistrict, Text: d.Name));
                else if (allow) into.Add(new AgentCommand(AgentIntent.AllowDistrict, Text: d.Name));
                else if (clause.Contains("prefiero") || clause.Contains("busca") || clause.Contains("quiero") || clause.Contains("solo") || clause.Contains("cerca de"))
                    into.Add(new AgentCommand(AgentIntent.AddDistrict, Text: d.Name));
            }
            return;
        }

        if (modalityOnly(clause)) return;

        if (removeRole)
            foreach (var role in Extract(clause, RoleRemoveCues))
                into.Add(new AgentCommand(AgentIntent.RemoveRole, Text: Title(role)));
        else if (addRole)
            foreach (var role in Extract(clause, RoleAddCues))
                into.Add(new AgentCommand(AgentIntent.AddRole, Text: Title(role)));
        else if (allow)
            foreach (var keyword in Extract(clause, AllowCues))
                into.Add(new AgentCommand(AgentIntent.AllowKeyword, Text: keyword));
        else if (negative && !IsScheduleOrCommute(clause))
            foreach (var keyword in Extract(clause, NegativeCues))
                into.Add(new AgentCommand(AgentIntent.ExcludeKeyword, Text: keyword));

        static bool modalityOnly(string c) => ModalityWord().IsMatch(c);
    }

    private static bool IsScheduleOrCommute(string clause) =>
        clause.Contains("sabado") || clause.Contains("domingo") || clause.Contains("fines de semana") || clause.Contains("viaj") || clause.Contains("lejos");

    private static IEnumerable<string> Extract(string clause, string[] cues)
    {
        var cue = cues.OrderByDescending(c => c.Length).FirstOrDefault(c => clause.StartsWith(c + " ") || clause == c);
        var rest = cue is null ? clause : clause[cue.Length..];

        foreach (var part in Regex.Split(rest, @"\s+y\s+|\s+o\s+"))
        {
            var words = part.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            while (words.Count > 0 && Fillers.Contains(words[0])) words.RemoveAt(0);
            while (words.Count > 0 && Fillers.Contains(words[^1])) words.RemoveAt(words.Count - 1);
            var phrase = string.Join(' ', words);
            if (phrase.Length >= 3 && phrase.Length <= 60) yield return phrase;
        }
    }

    private static string Title(string phrase) =>
        string.Join(' ', phrase.Split(' ').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));

    private static bool StartsWithAny(string clause, string[] cues) =>
        cues.Any(c => clause.StartsWith(c + " ") || clause == c || clause.Contains(" " + c + " "));

    // ---- district lookup ----------------------------------------------------------------------------------------

    private static District? FindDistrict(string text) => FindDistricts(text).FirstOrDefault();

    private static List<District> FindDistricts(string clause)
    {
        var padded = $" {clause} ";
        return LimaDistricts.All
            .Where(d => d.Aliases.Append(d.Name).Select(TextNormalizer.Normalize)
                .Any(alias => alias.Length > 0 && alias != "lima" && padded.Contains($" {alias} ")))
            .ToList();
    }
}
