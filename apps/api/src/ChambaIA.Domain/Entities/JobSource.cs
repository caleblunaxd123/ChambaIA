using ChambaIA.Domain.Enums;

namespace ChambaIA.Domain.Entities;

public class JobSource
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Stable identifier used by connectors (e.g. "demo", "computrabajo").</summary>
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public JobSourceKind Kind { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? BaseUrl { get; set; }
    public string? Notes { get; set; }
    /// <summary>Last successful fetch.</summary>
    public DateTimeOffset? LastFetchedAt { get; set; }
    /// <summary>Why the last attempt failed (null after a success). Shown to operators, never to candidates.</summary>
    public string? LastError { get; set; }
}
