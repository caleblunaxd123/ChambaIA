using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Matching;
using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Agent;

/// <summary>Applies a parsed command to the candidate's preferences. Idempotent: returns null when nothing changed.</summary>
public static class AgentCommandApplier
{
    public static string? Apply(JobPreferences prefs, AgentCommand command)
    {
        var summary = command.Intent switch
        {
            AgentIntent.SetMinSalary => SetMinSalary(prefs, command.Number!.Value),
            AgentIntent.AddRole => Add(prefs.PreferredRoles, command.Text!, $"Ahora también busco «{command.Text}»."),
            AgentIntent.RemoveRole => Remove(prefs.PreferredRoles, command.Text!, $"Dejé de buscar «{command.Text}»."),
            AgentIntent.AddDistrict => AddPreferredDistrict(prefs, command.Text!),
            AgentIntent.ExcludeDistrict => ExcludeDistrict(prefs, command.Text!),
            AgentIntent.AllowDistrict => Remove(prefs.ExcludedDistricts, command.Text!, $"Volveré a mostrarte trabajos en {command.Text}."),
            AgentIntent.SetHomeDistrict => SetHome(prefs, command.Text!),
            AgentIntent.SetModality => SetModality(prefs, command.Modality!.Value),
            AgentIntent.AddModality => AddModality(prefs, command.Modality!.Value),
            AgentIntent.RemoveModality => RemoveModality(prefs, command.Modality!.Value),
            AgentIntent.SetMaxCommute => SetCommute(prefs, (int)command.Number!.Value),
            AgentIntent.SetWeekdaysOnly => SetWeekdays(prefs, command.Flag!.Value),
            AgentIntent.ExcludeKeyword => Add(prefs.ExcludedKeywords, command.Text!, $"Ya no te mostraré ofertas de «{command.Text}»."),
            AgentIntent.AllowKeyword => AllowKeyword(prefs, command.Text!),
            _ => null
        };

        if (summary is not null) prefs.UpdatedAt = DateTimeOffset.UtcNow;
        return summary;
    }

    private static string? SetMinSalary(JobPreferences prefs, decimal amount)
    {
        if (prefs.MinSalary == amount) return null;
        prefs.MinSalary = amount;
        return $"Sueldo mínimo: {MatchFormatting.Money(amount)}.";
    }

    private static string? AddPreferredDistrict(JobPreferences prefs, string district)
    {
        prefs.ExcludedDistricts.RemoveAll(d => TextNormalizer.SameText(d, district));
        return Add(prefs.PreferredDistricts, district, $"Daré prioridad a {district}.");
    }

    private static string? ExcludeDistrict(JobPreferences prefs, string district)
    {
        prefs.PreferredDistricts.RemoveAll(d => TextNormalizer.SameText(d, district));
        return Add(prefs.ExcludedDistricts, district, $"Ya no verás trabajos en {district}.");
    }

    private static string? SetHome(JobPreferences prefs, string district)
    {
        if (TextNormalizer.SameText(prefs.HomeDistrict, district)) return null;
        prefs.HomeDistrict = district;
        return $"Calcularé los viajes desde {district}.";
    }

    private static string? SetModality(JobPreferences prefs, WorkModality modality)
    {
        if (prefs.PreferredModalities is [var only] && only == modality) return null;
        prefs.PreferredModalities = [modality];
        return $"Solo te mostraré trabajos {Label(modality)}.";
    }

    private static string? AddModality(JobPreferences prefs, WorkModality modality)
    {
        if (prefs.PreferredModalities.Count == 0 || prefs.PreferredModalities.Contains(modality)) return null;
        prefs.PreferredModalities.Add(modality);
        return $"También te mostraré trabajos {Label(modality)}.";
    }

    private static string? RemoveModality(JobPreferences prefs, WorkModality modality)
    {
        // An empty list means "any modality", so excluding one means keeping the other two.
        var current = prefs.PreferredModalities.Count == 0 ? Enum.GetValues<WorkModality>().ToList() : prefs.PreferredModalities;
        if (!current.Contains(modality)) return null;
        prefs.PreferredModalities = current.Where(m => m != modality).ToList();
        return $"Ya no verás trabajos {Label(modality)}.";
    }

    private static string? SetCommute(JobPreferences prefs, int minutes)
    {
        if (prefs.MaxCommuteMinutes == minutes) return null;
        prefs.MaxCommuteMinutes = minutes;
        var text = minutes % 60 == 0 ? (minutes == 60 ? "1 hora" : $"{minutes / 60} horas") : minutes == 90 ? "1 hora y media" : $"{minutes} minutos";
        return $"Viaje máximo: {text}.";
    }

    private static string? SetWeekdays(JobPreferences prefs, bool weekdaysOnly)
    {
        if (prefs.WeekdaysOnly == weekdaysOnly) return null;
        prefs.WeekdaysOnly = weekdaysOnly;
        return weekdaysOnly ? "Solo trabajos de lunes a viernes." : "También te mostraré trabajos con fines de semana.";
    }

    private static string? AllowKeyword(JobPreferences prefs, string keyword)
    {
        var removed = prefs.ExcludedKeywords.RemoveAll(k => TextNormalizer.SameText(k, keyword))
                      + prefs.ExcludedRoles.RemoveAll(k => TextNormalizer.SameText(k, keyword));
        return removed > 0 ? $"Volveré a mostrarte ofertas de «{keyword}»." : null;
    }

    private static string? Add(List<string> list, string value, string message)
    {
        if (list.Any(x => TextNormalizer.SameText(x, value))) return null;
        list.Add(value);
        return message;
    }

    private static string? Remove(List<string> list, string value, string message) =>
        list.RemoveAll(x => TextNormalizer.SameText(x, value)) > 0 ? message : null;

    private static string Label(WorkModality m) => m switch
    {
        WorkModality.OnSite => "presenciales",
        WorkModality.Hybrid => "híbridos",
        _ => "remotos"
    };
}
