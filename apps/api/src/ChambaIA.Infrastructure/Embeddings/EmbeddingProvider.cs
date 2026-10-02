using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChambaIA.Infrastructure.Embeddings;

public sealed class EmbeddingOptions
{
    public const string Section = "Embeddings";

    /// <summary>"None" (default: the product runs without vectors) or "Ollama".</summary>
    public string Provider { get; set; } = "None";

    /// <summary>Ollama base URL. A remote server must be reached over a private network or behind a token-checking proxy.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>bge-m3: multilingual with strong Spanish, 1024 dimensions (matches the vector(1024) columns).</summary>
    public string Model { get; set; } = "bge-m3";

    /// <summary>Must equal the dimension of the vector columns. A different model needs a migration, so this is enforced.</summary>
    public int Dimensions { get; set; } = 1024;

    /// <summary>Optional bearer token for a reverse proxy in front of Ollama (Ollama itself has no authentication).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Some models (multilingual-e5) need "passage: " (offers) and "query: " (candidate) prefixes; bge-m3 needs none.</summary>
    public string DocumentPrefix { get; set; } = "";
    public string QueryPrefix { get; set; } = "";

    public int BatchSize { get; set; } = 16;
    public int TimeoutSeconds { get; set; } = 60;
    /// <summary>Budget for embedding in the middle of a user request (profile save). Past it, the request simply goes without.</summary>
    public int InteractiveTimeoutSeconds { get; set; } = 8;

    /// <summary>Consecutive failures that open the circuit, and how long it stays open.</summary>
    public int FailuresBeforeOpen { get; set; } = 3;
    public int OpenForSeconds { get; set; } = 60;

    /// <summary>Cosine anchors for the 0-100 score; see <c>SemanticSimilarity</c>.</summary>
    public double UnrelatedCosine { get; set; } = Domain.Matching.SemanticSimilarity.UnrelatedCosine;
    public double IdenticalKindCosine { get; set; } = Domain.Matching.SemanticSimilarity.IdenticalKindCosine;

    public bool Enabled => !string.Equals(Provider, "None", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Raised when vectors cannot be produced right now. Callers treat it as "no semantic score", never as a failure.</summary>
public sealed class EmbeddingUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public interface IEmbeddingProvider
{
    string Model { get; }
    int Dimensions { get; }
    bool IsEnabled { get; }

    /// <summary>One vector per input, same order. Throws <see cref="EmbeddingUnavailableException"/> when it cannot.</summary>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct);
}

/// <summary>The "no AI" mode: matching keeps working with stages A and B only.</summary>
public sealed class NullEmbeddingProvider(IOptions<EmbeddingOptions> options) : IEmbeddingProvider
{
    public string Model => options.Value.Model;
    public int Dimensions => options.Value.Dimensions;
    public bool IsEnabled => false;

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct) =>
        throw new EmbeddingUnavailableException("Los embeddings están desactivados (Embeddings:Provider = None).");
}

/// <summary>Shared across scopes: remembers recent failures so a dead server is not hammered by every request.</summary>
public sealed class EmbeddingCircuit(TimeProvider clock)
{
    private readonly object _gate = new();
    private int _failures;
    private DateTimeOffset _openUntil = DateTimeOffset.MinValue;

    public bool IsOpen => clock.GetUtcNow() < _openUntil;

    public void Success()
    {
        lock (_gate) _failures = 0;
    }

    /// <returns>True when this failure just opened the circuit.</returns>
    public bool Failure(int threshold, TimeSpan openFor)
    {
        lock (_gate)
        {
            if (++_failures < threshold) return false;
            _failures = 0;
            _openUntil = clock.GetUtcNow() + openFor;
            return true;
        }
    }
}

/// <summary>
/// Local/remote Ollama (<c>POST /api/embed</c>). Cost per call is zero; the price is CPU/GPU of your own server.
/// One retry for transient errors, a hard timeout, a circuit breaker, and a dimension check so a wrong model can never
/// write vectors of the wrong size into the database.
/// </summary>
public sealed class OllamaEmbeddingProvider(
    HttpClient http,
    EmbeddingCircuit circuit,
    IOptions<EmbeddingOptions> options,
    ILogger<OllamaEmbeddingProvider> logger) : IEmbeddingProvider
{
    private readonly EmbeddingOptions _o = options.Value;

    public string Model => _o.Model;
    public int Dimensions => _o.Dimensions;
    public bool IsEnabled => true;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        if (texts.Count == 0) return [];
        if (circuit.IsOpen) throw new EmbeddingUnavailableException("El servidor de embeddings no responde; se reintentará en unos segundos.");

        Exception? last = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var vectors = await SendAsync(texts, ct);
                circuit.Success();
                return vectors;
            }
            // A wrong model/dimension (EmbeddingUnavailableException) will not fix itself: only network-level problems are retried.
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException && !ct.IsCancellationRequested)
            {
                last = ex;
                if (attempt == 1) await Task.Delay(TimeSpan.FromMilliseconds(400), ct);
            }
        }

        if (circuit.Failure(_o.FailuresBeforeOpen, TimeSpan.FromSeconds(_o.OpenForSeconds)))
            logger.LogWarning("Embedding server at {BaseUrl} failed repeatedly; pausing calls for {Seconds}s. Matching continues without semantic scores.", _o.BaseUrl, _o.OpenForSeconds);
        throw new EmbeddingUnavailableException("No pudimos contactar al servidor de embeddings.", last);
    }

    private async Task<IReadOnlyList<float[]>> SendAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/embed")
        {
            Content = JsonContent.Create(new EmbedRequest(_o.Model, texts.ToList()))
        };
        if (!string.IsNullOrWhiteSpace(_o.ApiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _o.ApiKey);

        using var response = await http.SendAsync(request, ct);
        if ((int)response.StatusCode >= 500) throw new HttpRequestException($"Embedding server answered {(int)response.StatusCode}.");
        if (!response.IsSuccessStatusCode)
            throw new EmbeddingUnavailableException(response.StatusCode == System.Net.HttpStatusCode.NotFound
                ? $"El modelo «{_o.Model}» no está instalado en el servidor (ejecuta: ollama pull {_o.Model})."
                : $"El servidor de embeddings rechazó la solicitud ({(int)response.StatusCode}).");

        var body = await response.Content.ReadFromJsonAsync<EmbedResponse>(ct)
                   ?? throw new EmbeddingUnavailableException("Respuesta vacía del servidor de embeddings.");
        if (body.Embeddings is null || body.Embeddings.Count != texts.Count)
            throw new EmbeddingUnavailableException("El servidor devolvió una cantidad inesperada de vectores.");
        if (body.Embeddings.Any(v => v.Length != _o.Dimensions))
            throw new EmbeddingUnavailableException(
                $"El modelo «{_o.Model}» devuelve {body.Embeddings[0].Length} dimensiones y la base espera {_o.Dimensions}. Usa bge-m3 o crea una migración para otro tamaño.");
        return body.Embeddings;
    }

    private sealed record EmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] List<string> Input,
        [property: JsonPropertyName("truncate")] bool Truncate = true);

    private sealed record EmbedResponse([property: JsonPropertyName("embeddings")] List<float[]>? Embeddings);
}
