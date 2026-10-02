using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Ingestion;

namespace ChambaIA.Infrastructure.Ingestion;

/// <summary>
/// One place offers come from. Connectors only fetch and translate to <see cref="RawJob"/>: normalisation, dedup and
/// storage are shared by all of them in <see cref="IngestionService"/>. See docs/JOB-SOURCES.md for the legal policy
/// every connector must follow before it is written.
/// </summary>
public interface IJobSource
{
    /// <summary>Stable key stored in <c>JobSource.Key</c> (e.g. "demo", "feed-clinica-x").</summary>
    string Key { get; }
    string Name { get; }
    JobSourceKind Kind { get; }
    string? BaseUrl { get; }

    /// <param name="since">Last successful fetch, for sources that support incremental reads (may be ignored).</param>
    Task<IReadOnlyList<RawJob>> FetchJobsAsync(DateTimeOffset? since, CancellationToken ct);
}
