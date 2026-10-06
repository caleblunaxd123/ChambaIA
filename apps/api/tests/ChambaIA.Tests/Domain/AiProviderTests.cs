using System.Net;
using System.Text;
using ChambaIA.Infrastructure.Ai;
using Microsoft.Extensions.Options;

namespace ChambaIA.Tests.Domain;

/// <summary>The three provider clients against a scripted server: request shape, headers, usage parsing, retries and failure modes.</summary>
public class AiProviderTests
{
    private sealed record Seen(string Method, string Url, string Body, IReadOnlyDictionary<string, string> Headers);

    private sealed class Server(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Seen> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var headers = request.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => string.Join(",", h.Value));
            Requests.Add(new Seen(request.Method.Method, request.RequestUri!.ToString(), await request.Content!.ReadAsStringAsync(ct), headers));
            return respond(Requests.Count);
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpClient Client(Server server, string baseUrl) => new(server) { BaseAddress = new Uri(baseUrl) };

    private static readonly AiRequest Ask = new("Eres un extractor.", "Dame los datos.", 300, Json: true);

    // ---- Ollama ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Ollama_gets_a_deterministic_json_chat_request_and_reports_its_token_counts()
    {
        var server = new Server(_ => Json("{\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"ok\\\":true}\"},\"prompt_eval_count\":120,\"eval_count\":30}"));
        var provider = new OllamaChatProvider(Client(server, "http://localhost:11434/"), new AiProviderOptions { Type = "Ollama", Model = "qwen2.5:3b" });

        var answer = await provider.CompleteAsync(Ask, default);

        Assert.Equal(("{\"ok\":true}", 120, 30), (answer.Text, answer.InputTokens, answer.OutputTokens));
        var seen = Assert.Single(server.Requests);
        Assert.Equal("http://localhost:11434/api/chat", seen.Url);
        using var body = System.Text.Json.JsonDocument.Parse(seen.Body);
        var root = body.RootElement;
        Assert.Equal("qwen2.5:3b", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("json", root.GetProperty("format").GetString());
        Assert.Equal(0, root.GetProperty("options").GetProperty("temperature").GetInt32());
        Assert.Equal(300, root.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.Equal(["system", "user"], root.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("role").GetString()));
    }

    [Fact]
    public async Task Ollama_without_json_mode_does_not_force_a_format()
    {
        var server = new Server(_ => Json("{\"message\":{\"content\":\"hola\"}}"));
        var provider = new OllamaChatProvider(Client(server, "http://localhost:11434/"), new AiProviderOptions { Model = "m" });

        await provider.CompleteAsync(Ask with { Json = false }, default);

        Assert.DoesNotContain("\"format\"", server.Requests[0].Body);
    }

    // ---- OpenAI-compatible -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task OpenAiCompatible_sends_a_bearer_key_to_the_chat_completions_path_under_the_base_url()
    {
        var server = new Server(_ => Json("{\"choices\":[{\"message\":{\"content\":\"{}\"}}],\"usage\":{\"prompt_tokens\":200,\"completion_tokens\":40}}"));
        var provider = new OpenAiCompatibleProvider(Client(server, "https://api.example.test/v1/"), new AiProviderOptions { Model = "mini", ApiKey = "sk-secret" });

        var answer = await provider.CompleteAsync(Ask, default);

        Assert.Equal((200, 40), (answer.InputTokens, answer.OutputTokens));
        var seen = Assert.Single(server.Requests);
        Assert.Equal("https://api.example.test/v1/chat/completions", seen.Url);
        Assert.Equal("Bearer sk-secret", seen.Headers["authorization"]);
        Assert.Contains("\"response_format\":{\"type\":\"json_object\"}", seen.Body);
        Assert.Contains("\"max_tokens\":300", seen.Body);
    }

    // ---- Anthropic -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Anthropic_uses_its_own_headers_the_system_field_and_joins_the_text_blocks()
    {
        var server = new Server(_ => Json("{\"content\":[{\"type\":\"text\",\"text\":\"{\\\"a\\\":\"},{\"type\":\"tool_use\",\"id\":\"x\"},{\"type\":\"text\",\"text\":\"1}\"}],\"usage\":{\"input_tokens\":90,\"output_tokens\":12}}"));
        var provider = new AnthropicProvider(Client(server, "https://api.anthropic.example/"), new AiProviderOptions { Model = "claude-model", ApiKey = "key-123" });

        var answer = await provider.CompleteAsync(Ask, default);

        Assert.Equal(("{\"a\":1}", 90, 12), (answer.Text, answer.InputTokens, answer.OutputTokens));
        var seen = Assert.Single(server.Requests);
        Assert.Equal("https://api.anthropic.example/v1/messages", seen.Url);
        Assert.Equal("key-123", seen.Headers["x-api-key"]);
        Assert.Equal("2023-06-01", seen.Headers["anthropic-version"]);
        Assert.False(seen.Headers.ContainsKey("authorization"));
        Assert.Contains("\"system\":\"Eres un extractor.\"", seen.Body);
    }

    // ---- failure modes (all providers share them) ---------------------------------------------------------------------------

    private static OpenAiCompatibleProvider OpenAi(Server server) =>
        new(Client(server, "https://api.example.test/v1/"), new AiProviderOptions { Model = "mini", ApiKey = "sk-very-secret" });

    private const string Good = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";

    [Fact]
    public async Task A_rate_limit_or_server_error_is_retried_once()
    {
        var server = new Server(n => n == 1 ? Json("{}", HttpStatusCode.TooManyRequests) : Json(Good));

        var answer = await OpenAi(server).CompleteAsync(Ask, default);

        Assert.Equal("ok", answer.Text);
        Assert.Equal(2, server.Requests.Count);
    }

    [Fact]
    public async Task A_rejected_key_fails_at_once_without_retrying_and_never_leaks_the_key_or_the_prompt()
    {
        var server = new Server(_ => Json("{\"error\":\"bad key sk-very-secret Dame los datos\"}", HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<AiProviderException>(() => OpenAi(server).CompleteAsync(Ask, default));

        Assert.Equal(401, ex.StatusCode);
        Assert.Single(server.Requests);
        Assert.DoesNotContain("sk-very-secret", ex.ToString());
        Assert.DoesNotContain("Dame los datos", ex.ToString());
    }

    [Fact]
    public async Task Two_server_errors_in_a_row_give_up()
    {
        var server = new Server(_ => Json("{}", HttpStatusCode.BadGateway));

        var ex = await Assert.ThrowsAsync<AiProviderException>(() => OpenAi(server).CompleteAsync(Ask, default));

        Assert.Equal(502, ex.StatusCode);
        Assert.Equal(2, server.Requests.Count);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":42}}]}")]
    public async Task A_reply_in_an_unexpected_shape_is_an_error_not_a_crash(string reply)
    {
        var server = new Server(_ => Json(reply));

        await Assert.ThrowsAsync<AiProviderException>(() => OpenAi(server).CompleteAsync(Ask, default));
    }

    [Fact]
    public async Task An_unreachable_server_becomes_a_provider_error()
    {
        var provider = new OpenAiCompatibleProvider(
            new HttpClient(new Server(_ => throw new HttpRequestException("connection refused"))) { BaseAddress = new Uri("https://api.example.test/v1/") },
            new AiProviderOptions { Model = "mini" });

        await Assert.ThrowsAsync<AiProviderException>(() => provider.CompleteAsync(Ask, default));
    }

    // ---- resolver --------------------------------------------------------------------------------------------------------

    private static AiProviderResolver Resolver(Dictionary<string, AiProviderOptions> providers) =>
        new(new Factory(new Server(_ => Json(Good))), Options.Create(new AiOptions { Providers = new(providers, StringComparer.OrdinalIgnoreCase) }));

    [Fact]
    public void The_resolver_builds_the_right_client_for_each_configured_type()
    {
        var resolver = Resolver(new()
        {
            ["local"] = new() { Type = "Ollama", Model = "qwen" },
            ["cheap"] = new() { Type = "OpenAICompatible", BaseUrl = "https://api.example.test/v1", Model = "mini" },
            ["claude"] = new() { Type = "anthropic", Model = "claude-model" }
        });

        Assert.IsType<OllamaChatProvider>(resolver.Get("local"));
        Assert.IsType<OpenAiCompatibleProvider>(resolver.Get("CHEAP"));
        Assert.IsType<AnthropicProvider>(resolver.Get("claude"));
    }

    [Fact]
    public void Misconfigured_providers_resolve_to_nothing_instead_of_failing_later()
    {
        var resolver = Resolver(new()
        {
            ["nomodel"] = new() { Type = "Ollama", Model = "" },
            ["nobase"] = new() { Type = "OpenAICompatible", Model = "mini" },
            ["weird"] = new() { Type = "Skynet", Model = "x" }
        });

        Assert.Null(resolver.Get("nomodel"));
        Assert.Null(resolver.Get("nobase"));
        Assert.Null(resolver.Get("weird"));
        Assert.Null(resolver.Get("missing"));
    }
}
