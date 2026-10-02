using System.Net;
using System.Text;
using System.Text.Json;
using ChambaIA.Infrastructure.Embeddings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChambaIA.Tests.Domain;

/// <summary>The provider is exercised against a scripted HTTP handler: no Ollama, no network, deterministic.</summary>
public class OllamaProviderTests
{
    private sealed class ScriptedHandler(Func<HttpRequestMessage, int, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<(string Path, string Body, string? Auth)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Requests.Add((request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(ct), request.Headers.Authorization?.ToString()));
            return await respond(request, Calls);
        }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private static object Vectors(int count, int dims) => new { embeddings = Enumerable.Range(0, count).Select(_ => Enumerable.Repeat(0.5f, dims).ToArray()).ToArray() };

    private static (OllamaEmbeddingProvider Provider, EmbeddingCircuit Circuit, Clock Clock) Build(ScriptedHandler handler, Action<EmbeddingOptions>? tweak = null)
    {
        var options = new EmbeddingOptions { Provider = "Ollama", Dimensions = 8, FailuresBeforeOpen = 2, OpenForSeconds = 60 };
        tweak?.Invoke(options);
        var clock = new Clock();
        var circuit = new EmbeddingCircuit(clock);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://ollama.test/") };
        return (new OllamaEmbeddingProvider(http, circuit, Options.Create(options), NullLogger<OllamaEmbeddingProvider>.Instance), circuit, clock);
    }

    [Fact]
    public async Task Sends_the_documented_request_and_returns_one_vector_per_text()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, Vectors(2, 8))));
        var (provider, _, _) = Build(handler, o => o.ApiKey = "secret-token");

        var vectors = await provider.EmbedAsync(["asistente administrativa", "cajera"], default);

        Assert.Equal(2, vectors.Count);
        Assert.All(vectors, v => Assert.Equal(8, v.Length));
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/embed", request.Path);
        Assert.Equal("Bearer secret-token", request.Auth);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("bge-m3", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("input").GetArrayLength());
    }

    [Fact]
    public async Task A_model_with_the_wrong_dimension_is_rejected_instead_of_corrupting_the_database()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, Vectors(1, 384))));
        var (provider, _, _) = Build(handler);

        var ex = await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => provider.EmbedAsync(["x"], default));

        Assert.Contains("384", ex.Message);
        Assert.Equal(1, handler.Calls); // not retried: it will not fix itself
    }

    [Fact]
    public async Task A_missing_model_says_how_to_install_it()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.NotFound, new { error = "model not found" })));
        var (provider, _, _) = Build(handler);

        var ex = await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => provider.EmbedAsync(["x"], default));

        Assert.Contains("ollama pull bge-m3", ex.Message);
    }

    [Fact]
    public async Task A_transient_server_error_is_retried_once()
    {
        var handler = new ScriptedHandler((_, call) => Task.FromResult(call == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Json(HttpStatusCode.OK, Vectors(1, 8))));
        var (provider, _, _) = Build(handler);

        var vectors = await provider.EmbedAsync(["x"], default);

        Assert.Single(vectors);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task After_repeated_failures_the_circuit_opens_and_stops_calling_the_server_until_it_cools_down()
    {
        var handler = new ScriptedHandler((_, _) => throw new HttpRequestException("connection refused"));
        var (provider, circuit, clock) = Build(handler);

        for (var i = 0; i < 2; i++)
            await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => provider.EmbedAsync(["x"], default));
        Assert.True(circuit.IsOpen);

        var callsWhenOpened = handler.Calls;
        await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => provider.EmbedAsync(["x"], default));
        Assert.Equal(callsWhenOpened, handler.Calls); // short-circuited: the dead server is not hammered

        clock.Now = clock.Now.AddSeconds(61);
        Assert.False(circuit.IsOpen);
    }

    [Fact]
    public async Task A_success_resets_the_failure_count()
    {
        var fail = true;
        var handler = new ScriptedHandler((_, _) => fail ? throw new HttpRequestException("down") : Task.FromResult(Json(HttpStatusCode.OK, Vectors(1, 8))));
        var (provider, circuit, _) = Build(handler, o => o.FailuresBeforeOpen = 3);

        await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => provider.EmbedAsync(["x"], default));
        fail = false;
        await provider.EmbedAsync(["x"], default);
        fail = true;
        await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => provider.EmbedAsync(["x"], default));

        Assert.False(circuit.IsOpen); // 1 failure, then success, then 1 failure: never 3 in a row
    }

    [Fact]
    public async Task The_disabled_provider_always_reports_unavailable()
    {
        var provider = new NullEmbeddingProvider(Options.Create(new EmbeddingOptions()));

        Assert.False(provider.IsEnabled);
        await Assert.ThrowsAsync<EmbeddingUnavailableException>(() => provider.EmbedAsync(["x"], default));
    }
}
