namespace ChambaIA.Domain.Notifications;

public sealed record NotificationMessage(string Title, string Body, Guid? JobId);

/// <summary>Spanish copy for the notifications. Specific and calm: what was found, how good it is, no hype, no urgency tricks.</summary>
public static class NotificationCopy
{
    public static NotificationMessage Compose(IReadOnlyList<NotifiableMatch> matches, DateTimeOffset now)
    {
        // Best first: strong fits before the rest, then by score, then the newest.
        var best = matches
            .OrderByDescending(m => m.IsStrong)
            .ThenByDescending(m => m.Score)
            .ThenByDescending(m => m.PostedAt)
            .First();
        var strong = matches.Count(m => m.IsStrong);

        if (matches.Count == 1)
            return new NotificationMessage(
                "Nueva oportunidad para ti",
                $"{best.Title} en {best.Company} apareció {Ago(best.PostedAt, now)}. Encaja muy bien contigo.",
                best.JobId);

        var body = strong == 1
            ? $"Una encaja muy bien contigo: «{best.Title}» en {best.Company}."
            : $"{strong} encajan muy bien contigo, como «{best.Title}» en {best.Company}.";
        return new NotificationMessage($"Encontramos {matches.Count} oportunidades nuevas", body, best.JobId);
    }

    public static NotificationMessage Test() =>
        new("Tu agente está al tanto", "Así te avisaremos cuando aparezca algo que encaje contigo. Sin bombardearte.", null);

    /// <summary>"hace 15 minutos", "hace 3 horas", "hoy", "ayer".</summary>
    public static string Ago(DateTimeOffset? postedAt, DateTimeOffset now)
    {
        if (postedAt is null) return "hoy";
        var span = now - postedAt.Value;
        if (span < TimeSpan.Zero || span < TimeSpan.FromMinutes(1)) return "hace un momento";
        if (span < TimeSpan.FromHours(1)) return $"hace {(int)span.TotalMinutes} {((int)span.TotalMinutes == 1 ? "minuto" : "minutos")}";
        if (span < TimeSpan.FromHours(24)) return $"hace {(int)span.TotalHours} {((int)span.TotalHours == 1 ? "hora" : "horas")}";
        return span < TimeSpan.FromHours(48) ? "ayer" : $"hace {(int)span.TotalDays} días";
    }
}
