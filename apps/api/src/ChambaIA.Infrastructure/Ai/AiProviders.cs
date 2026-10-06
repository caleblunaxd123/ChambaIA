using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ChambaIA.Infrastructure.Ai;

public sealed record AiRequest(string System, string User, int MaxOutputTokens, bool Json);

public sealed record AiResponse(string Text, int InputTokens, int OutputTokens);

/// <summary>A provider could not answer (network, rate limit, server error, unreadable reply). The router moves on to the next one.</summary>
public sealed class AiProviderException(string message, int? statusCode = null, Exception? inner = null) : Exception(message, inner)
{
    public int? StatusCode { get; } = statusCode;
}

public interface IAiProvider
{
    string Type { get; }
    string Model { get; }
    Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct);
}

/// <summary>Shared HTTP plumbing: one retry for rate limits, server errors and network blips; error messages never carry keys or prompts.</summary>
internal static class AiHttp
{
    public static async Task<JsonDocument> PostAsync(HttpClient http, string path, object body, Action<HttpRequestMessage>? configure, CancellationToken ct)
    {
        AiProviderException? last = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
                configure?.Invoke(request);
                using var response = await http.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                    return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

                var code = (int)response.StatusCode;
                last = new AiProviderException($"El proveedor respondió HTTP {code}.", code);
                if (response.StatusCode != HttpStatusCode.TooManyRequests && code < 500) throw last; // a bad key or request will not improve
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
            {
                last = new AiProviderException("No se pudo contactar o leer la respuesta del proveedor.", null, ex);
            }

            if (attempt == 1) await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }
        throw last!;
    }

    public static string Required(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var step in path)
        {
            if (current.ValueKind == JsonValueKind.Array && int.TryParse(step, out var i) && i < current.GetArrayLength()) current = current[i];
            else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(step, out var next)) current = next;
            else throw new AiProviderException("La respuesta del proveedor no tiene el formato esperado.");
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() ?? "" : throw new AiProviderException("La respuesta del proveedor no tiene el formato esperado.");
    }

    public static int Int(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var step in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(step, out current)) return 0;
        }
        return current.ValueKind == JsonValueKind.Number && current.TryGetInt32(out var n) ? n : 0;
    }
}

/// <summary>A local model served by Ollama (<c>POST /api/chat</c>): zero cost per call, the price is your own hardware.</summary>
public sealed class OllamaChatProvider(HttpClient http, AiProviderOptions options) : IAiProvider
{
    public string Type => "Ollama";
    public string Model => options.Model;

    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = options.Model,
            ["stream"] = false,
            ["messages"] = new[] { new { role = "system", content = request.System }, new { role = "user", content = request.User } },
            ["options"] = new { temperature = 0, num_predict = request.MaxOutputTokens }
        };
        if (request.Json) body["format"] = "json";

        using var doc = await AiHttp.PostAsync(http, "api/chat", body, r =>
        {
            if (!string.IsNullOrWhiteSpace(options.ApiKey)) r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }, ct);

        return new AiResponse(
            AiHttp.Required(doc.RootElement, "message", "content"),
            AiHttp.Int(doc.RootElement, "prompt_eval_count"),
            AiHttp.Int(doc.RootElement, "eval_count"));
    }
}

/// <summary>
/// The de-facto standard chat API (<c>POST {BaseUrl}/chat/completions</c>): OpenAI, DeepSeek, Gemini's compatible endpoint, Groq,
/// OpenRouter and most local servers. One implementation covers every "cheap model" you may want to try.
/// </summary>
public sealed class OpenAiCompatibleProvider(HttpClient http, AiProviderOptions options) : IAiProvider
{
    public string Type => "OpenAICompatible";
    public string Model => options.Model;

    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = options.Model,
            ["temperature"] = 0,
            ["max_tokens"] = request.MaxOutputTokens,
            ["messages"] = new[] { new { role = "system", content = request.System }, new { role = "user", content = request.User } }
        };
        if (request.Json) body["response_format"] = new { type = "json_object" };

        using var doc = await AiHttp.PostAsync(http, "chat/completions", body, r =>
        {
            if (!string.IsNullOrWhiteSpace(options.ApiKey)) r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }, ct);

        return new AiResponse(
            AiHttp.Required(doc.RootElement, "choices", "0", "message", "content"),
            AiHttp.Int(doc.RootElement, "usage", "prompt_tokens"),
            AiHttp.Int(doc.RootElement, "usage", "completion_tokens"));
    }
}

/// <summary>Claude through the Messages API (<c>POST {BaseUrl}/v1/messages</c>). The model id comes from configuration.</summary>
public sealed class AnthropicProvider(HttpClient http, AiProviderOptions options) : IAiProvider
{
    public string Type => "Anthropic";
    public string Model => options.Model;

    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        var body = new
        {
            model = options.Model,
            max_tokens = request.MaxOutputTokens,
            temperature = 0,
            system = request.System,
            messages = new[] { new { role = "user", content = request.User } }
        };

        using var doc = await AiHttp.PostAsync(http, "v1/messages", body, r =>
        {
            if (!string.IsNullOrWhiteSpace(options.ApiKey)) r.Headers.Add("x-api-key", options.ApiKey);
            r.Headers.Add("anthropic-version", "2023-06-01");
        }, ct);

        // The reply is a list of content blocks; only the text ones matter here.
        var text = new StringBuilder();
        if (doc.RootElement.TryGetProperty("content", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
            foreach (var block in blocks.EnumerateArray())
                if (block.TryGetProperty("type", out var t) && t.GetString() == "text" && block.TryGetProperty("text", out var s) && s.ValueKind == JsonValueKind.String)
                    text.Append(s.GetString());
        if (text.Length == 0) throw new AiProviderException("La respuesta del proveedor no tiene el formato esperado.");

        return new AiResponse(text.ToString(), AiHttp.Int(doc.RootElement, "usage", "input_tokens"), AiHttp.Int(doc.RootElement, "usage", "output_tokens"));
    }
}

public interface IAiProviderResolver
{
    /// <summary>The provider configured under this name, or null when the name is unknown or its type is not supported.</summary>
    IAiProvider? Get(string name);
}

public sealed class AiProviderResolver(IHttpClientFactory httpFactory, IOptions<AiOptions> options) : IAiProviderResolver
{
    public IAiProvider? Get(string name)
    {
        var o = options.Value;
        if (!o.Providers.TryGetValue(name, out var p) || string.IsNullOrWhiteSpace(p.Model)) return null;

        var http = httpFactory.CreateClient("ai");
        http.Timeout = TimeSpan.FromSeconds(Math.Max(5, p.TimeoutSeconds > 0 ? p.TimeoutSeconds : o.TimeoutSeconds));

        // A trailing slash makes the relative request paths append to the base path instead of replacing its last segment.
        string Base(string fallback) => (string.IsNullOrWhiteSpace(p.BaseUrl) ? fallback : p.BaseUrl).TrimEnd('/') + "/";
        switch (p.Type.ToLowerInvariant())
        {
            case "ollama":
                http.BaseAddress = new Uri(Base("http://localhost:11434"));
                return new OllamaChatProvider(http, p);
            case "openaicompatible":
                if (string.IsNullOrWhiteSpace(p.BaseUrl)) return null;
                http.BaseAddress = new Uri(Base(p.BaseUrl));
                return new OpenAiCompatibleProvider(http, p);
            case "anthropic":
                http.BaseAddress = new Uri(Base("https://api.anthropic.com"));
                return new AnthropicProvider(http, p);
            default:
                return null;
        }
    }
}
