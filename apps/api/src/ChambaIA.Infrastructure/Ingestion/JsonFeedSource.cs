using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Ingestion;
using Microsoft.Extensions.Logging;

namespace ChambaIA.Infrastructure.Ingestion;

/// <summary>Configuration of one JSON feed (employer careers feed, partner export, public open-data endpoint).</summary>
public sealed class JsonFeedOptions
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public bool Enabled { get; set; } = true;
    /// <summary>Who allowed us to read it and under which terms (required: no feed without a documented permission).</summary>
    public string? Permission { get; set; }
}

/// <summary>
/// Reads the ChambaIA JSON feed format (documented in docs/JOB-SOURCES.md). Feeds are the preferred legal path after
/// official APIs: the publisher decides what to share. Polite by design: identifiable User-Agent, conditional GET with
/// ETag, timeout, and one bad item never discards the rest of the feed.
/// </summary>
public sealed class JsonFeedSource(JsonFeedOptions options, IHttpClientFactory httpFactory, ILogger<JsonFeedSource> logger) : IJobSource
{
    public const string HttpClientName = "job-feeds";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { NumberHandling = JsonNumberHandling.AllowReadingFromString };
    private string? _etag;
    private IReadOnlyList<RawJob> _cached = [];

    public string Key => options.Key;
    public string Name => options.Name;
    public JobSourceKind Kind => JobSourceKind.Feed;
    public string? BaseUrl => options.Url;

    public async Task<IReadOnlyList<RawJob>> FetchJobsAsync(DateTimeOffset? since, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Permission))
            throw new InvalidOperationException("Este feed no tiene un permiso documentado (Ingestion:Feeds:*:Permission); no se lee.");

        var http = httpFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, options.Url);
        if (_etag is not null) request.Headers.TryAddWithoutValidation("If-None-Match", _etag);

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotModified) return _cached;
        response.EnsureSuccessStatusCode();

        var feed = await response.Content.ReadFromJsonAsync<FeedDocument>(Json, ct)
            ?? throw new InvalidDataException("El feed está vacío.");

        var jobs = new List<RawJob>();
        foreach (var item in feed.Jobs ?? [])
        {
            if (item.ToRawJob() is { } raw) jobs.Add(raw);
            else logger.LogWarning("Feed {Key}: skipped an item without id/title/company/url.", Key);
        }

        _etag = response.Headers.ETag?.ToString();
        _cached = jobs;
        return jobs;
    }

    /// <summary>The public feed format. Everything except id, title, company, description and url is optional.</summary>
    public sealed class FeedDocument
    {
        public List<FeedItem>? Jobs { get; set; }
    }

    public sealed class FeedItem
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Company { get; set; }
        public string? Description { get; set; }
        public string? Url { get; set; }
        public string? Location { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string? Salary { get; set; }
        public string? Modality { get; set; }
        public string? EmploymentType { get; set; }
        public string? Schedule { get; set; }
        public string? Industry { get; set; }
        public DateTimeOffset? PostedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public List<string>? Skills { get; set; }
        public List<string>? NiceToHave { get; set; }

        public RawJob? ToRawJob()
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Company) || string.IsNullOrWhiteSpace(Url))
                return null;

            return new RawJob
            {
                ExternalId = Id,
                Title = Title,
                Company = Company,
                Description = Description ?? "",
                Url = Url,
                Location = Location,
                SalaryMin = SalaryMin,
                SalaryMax = SalaryMax,
                SalaryText = Salary,
                Modality = Modality,
                EmploymentType = EmploymentType,
                Schedule = Schedule,
                Industry = Industry,
                PostedAt = PostedAt,
                ExpiresAt = ExpiresAt,
                // A feed that lists skills is authoritative; otherwise the normaliser reads them from the description.
                SkillsRequired = Skills?.Select(s => new RawSkill(s)).ToList(),
                SkillsPreferred = Skills is null ? null : NiceToHave ?? []
            };
        }
    }
}
