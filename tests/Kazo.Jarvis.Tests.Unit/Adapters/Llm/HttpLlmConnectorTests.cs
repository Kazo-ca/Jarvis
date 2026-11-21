using System.Net;
using System.Text.Json;
using Kazo.Jarvis.Adapters.Llm;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Tests.Unit.Adapters.Secrets;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Llm;

/// <summary>The three hand-written adapters (ADR 0007, section 2) against servers in memory.</summary>
[TestFixture]
public sealed partial class HttpLlmConnectorTests
{
    private static readonly SecretRef KeyRef = new("llm.test.apikey");

    private static LlmProvider Ollama(string model = "qwen3:14b", string address = "http://127.0.0.1:11435", LlmTuning? tuning = null) =>
        new("local", "Ollama", LlmProviderKind.Ollama, new Uri(address), model, tuning ?? new LlmTuning(), KeyRef);

    private static LlmProvider Server(LlmProviderKind kind, string address, string model) =>
        new("test", "Test", kind, new Uri(address), model, new LlmTuning(), KeyRef);

    private static (HttpLlmConnector Connector, StubHandler Handler, FakeVault Vault) Create(Func<HttpRequestMessage, string, HttpResponseMessage> answer)
    {
        var handler = new StubHandler(answer);
        var vault = new FakeVault();
        return (new HttpLlmConnector(new HttpClient(handler), vault), handler, vault);
    }

    private static List<ChatMessage> Question() =>
        [new(ChatRole.System, "Vous êtes Jarvis."), new(ChatRole.User, "Quelle heure est-il ?")];

    [Test]
    public async Task Ollama_streams_the_answer_with_the_voice_settings()
    {
        const string stream = """
            {"model":"qwen3:14b","message":{"role":"assistant","content":"Il est "},"done":false}
            {"model":"qwen3:14b","message":{"role":"assistant","content":"midi, Monsieur."},"done":false}
            {"model":"qwen3:14b","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","prompt_eval_count":31,"eval_count":7}
            """;
        var (connector, handler, _) = Create((_, _) => StubHandler.Stream(stream));
        using var client = connector.CreateClient(Ollama());

        var texts = new List<string>();
        await foreach (var update in client.GetStreamingResponseAsync(Question()))
        {
            if (update.Text.Length > 0)
            {
                texts.Add(update.Text);
            }
        }

        var response = await client.GetResponseAsync(Question());
        using var body = JsonDocument.Parse(handler.Received[0].Body);
        var root = body.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(texts, Is.EqualTo(new[] { "Il est ", "midi, Monsieur." }), "The answer arrives piece by piece.");
            Assert.That(response.Text, Is.EqualTo("Il est midi, Monsieur."));
            Assert.That(response.ModelId, Is.EqualTo("qwen3:14b"));
            Assert.That(response.FinishReason, Is.EqualTo(ChatFinishReason.Stop));
            Assert.That(response.Usage?.InputTokenCount, Is.EqualTo(31));
            Assert.That(response.Usage?.OutputTokenCount, Is.EqualTo(7));
            Assert.That(handler.Received[0].Request.RequestUri, Is.EqualTo(new Uri("http://127.0.0.1:11435/api/chat")));
            Assert.That(root.GetProperty("think").GetBoolean(), Is.False, "Reasoning is off (ADR 0007, section 5).");
            Assert.That(root.GetProperty("keep_alive").GetInt32(), Is.EqualTo(-1), "The model stays in memory.");
            Assert.That(root.GetProperty("stream").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("options").GetProperty("num_ctx").GetInt32(), Is.EqualTo(4096));
            Assert.That(root.GetProperty("options").GetProperty("temperature").GetDouble(), Is.EqualTo(0.3));
            Assert.That(root.GetProperty("messages")[0].GetProperty("role").GetString(), Is.EqualTo("system"));
            Assert.That(root.GetProperty("messages")[1].GetProperty("content").GetString(), Is.EqualTo("Quelle heure est-il ?"));
        });
    }

    [Test]
    public async Task Ollama_sends_again_without_think_to_a_model_that_cannot_reason_and_remembers_it()
    {
        var (connector, handler, _) = Create((_, body) => body.Contains("\"think\"", StringComparison.Ordinal)
            ? StubHandler.Json("""{"error":"\"qwen2.5:14b\" does not support thinking"}""", HttpStatusCode.BadRequest)
            : StubHandler.Stream("""{"message":{"content":"Bien."},"done":true}"""));
        using var client = connector.CreateClient(Ollama("qwen2.5:14b"));

        var first = await client.GetResponseAsync(Question());
        var second = await client.GetResponseAsync(Question());

        Assert.Multiple(() =>
        {
            Assert.That(first.Text, Is.EqualTo("Bien."));
            Assert.That(second.Text, Is.EqualTo("Bien."));
            Assert.That(handler.Received, Has.Count.EqualTo(3), "One refusal, then two requests without think.");
        });
    }

    [Test]
    public void Ollama_names_a_missing_model()
    {
        var (connector, _, _) = Create((_, _) => StubHandler.Json("""{"error":"model 'qwen9' not found"}""", HttpStatusCode.NotFound));
        using var client = connector.CreateClient(Ollama("qwen9"));

        var exception = Assert.ThrowsAsync<LlmException>(() => client.GetResponseAsync(Question()));

        Assert.That(exception.Failure, Is.EqualTo(LlmFailure.ModelMissing));
    }

    [Test]
    public void A_server_that_does_not_answer_is_unreachable()
    {
        var (connector, _, _) = Create((_, _) => throw new HttpRequestException("Connection refused"));
        using var client = connector.CreateClient(Ollama());

        var exception = Assert.ThrowsAsync<LlmException>(() => client.GetResponseAsync(Question()));

        Assert.That(exception.Failure, Is.EqualTo(LlmFailure.Unreachable));
    }

    [Test]
    public void A_server_too_slow_times_out_but_a_cancellation_stays_a_cancellation()
    {
        var (connector, _, _) = Create((_, _) => throw new TaskCanceledException("HttpClient.Timeout"));
        using var client = connector.CreateClient(Ollama());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var exception = Assert.ThrowsAsync<LlmException>(() => client.GetResponseAsync(Question()));

        Assert.Multiple(() =>
        {
            Assert.That(exception.Failure, Is.EqualTo(LlmFailure.TimedOut));
            Assert.That(() => client.GetResponseAsync(Question(), cancellationToken: cancelled.Token), Throws.InstanceOf<OperationCanceledException>());
        });
    }

    [Test]
    public async Task The_connection_test_lists_the_models_and_names_a_missing_one()
    {
        var (connector, handler, _) = Create((_, _) => StubHandler.Json("""{"models":[{"name":"qwen3:14b","model":"qwen3:14b"},{"name":"mistral:latest"}]}"""));

        var passed = await connector.TestConnectionAsync(Ollama());
        var latest = await connector.TestConnectionAsync(Ollama("mistral"));
        var missing = await connector.TestConnectionAsync(Ollama("llama3:8b"));

        Assert.Multiple(() =>
        {
            Assert.That(handler.Received[0].Request.RequestUri, Is.EqualTo(new Uri("http://127.0.0.1:11435/api/tags")));
            Assert.That(passed.Succeeded, Is.True);
            Assert.That(passed.AvailableModels, Is.EqualTo(new[] { "qwen3:14b", "mistral:latest" }));
            Assert.That(latest.Succeeded, Is.True, "Ollama adds :latest to a name without a tag.");
            Assert.That(missing.Failure, Is.EqualTo(LlmFailure.ModelMissing));
            Assert.That(missing.AvailableModels, Has.Count.EqualTo(2), "The list helps pick the right name.");
        });
    }

    [Test]
    public async Task The_connection_test_names_each_cause()
    {
        var (unreachable, _, _) = Create((_, _) => throw new HttpRequestException("refused"));
        var (refused, _, refusedVault) = Create((_, _) => StubHandler.Json("""{"type":"error"}""", HttpStatusCode.Unauthorized));
        await refusedVault.SetAsync(KeyRef, new SecretValue("sk-wrong"));
        var (noKey, noKeyHandler, _) = Create((_, _) => StubHandler.Json("{}"));

        var results = new[]
        {
            await unreachable.TestConnectionAsync(Ollama()),
            await refused.TestConnectionAsync(Server(LlmProviderKind.Anthropic, "https://api.anthropic.com", "claude-opus-5-5")),
            await noKey.TestConnectionAsync(Server(LlmProviderKind.OpenAi, "https://api.openai.com/v1", "gpt-5")),
            await noKey.TestConnectionAsync(Ollama(address: "ftp://127.0.0.1")),
        };

        Assert.Multiple(() =>
        {
            Assert.That(results.Select(r => r.Failure), Is.EqualTo(new LlmFailure?[]
            {
                LlmFailure.Unreachable, LlmFailure.KeyRejected, LlmFailure.KeyMissing, LlmFailure.InvalidAddress,
            }));
            Assert.That(noKeyHandler.Received, Is.Empty, "Nothing leaves the PC without a key or with a bad address.");
        });
    }

    [Test]
    public async Task Ollama_says_whether_the_model_is_in_memory_and_preloads_it_with_the_chat_context()
    {
        var loaded = false;
        var (connector, handler, _) = Create((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/ps")
            {
                return StubHandler.Json(loaded ? """{"models":[{"name":"qwen3:14b","model":"qwen3:14b"}]}""" : """{"models":[]}""");
            }

            loaded = true;
            return StubHandler.Json("""{"model":"qwen3:14b","done":true,"done_reason":"load"}""");
        });

        var before = await connector.IsLoadedAsync(Ollama());
        await connector.PreloadAsync(Ollama());
        var after = await connector.IsLoadedAsync(Ollama());
        var other = await connector.IsLoadedAsync(Server(LlmProviderKind.OpenAiCompatible, "http://127.0.0.1:8080", "default"));

        using var preload = JsonDocument.Parse(handler.Received[1].Body);
        Assert.Multiple(() =>
        {
            Assert.That(before, Is.False);
            Assert.That(after, Is.True);
            Assert.That(other, Is.Null, "Only Ollama can tell.");
            Assert.That(handler.Received[1].Request.RequestUri!.AbsolutePath, Is.EqualTo("/api/chat"));
            Assert.That(preload.RootElement.GetProperty("messages").GetArrayLength(), Is.Zero);
            Assert.That(preload.RootElement.GetProperty("keep_alive").GetInt32(), Is.EqualTo(-1));
            Assert.That(preload.RootElement.GetProperty("options").GetProperty("num_ctx").GetInt32(), Is.EqualTo(4096),
                "The same context as the chats, or Ollama loads the model again at the first question.");
        });
    }

    [Test]
    public async Task An_openai_compatible_server_streams_server_sent_events()
    {
        const string stream = """
            data: {"model":"qwen2.5-7b","choices":[{"delta":{"role":"assistant","content":"Bon"}}]}

            data: {"model":"qwen2.5-7b","choices":[{"delta":{"content":"soir."},"finish_reason":"stop"}]}

            data: {"model":"qwen2.5-7b","choices":[],"usage":{"prompt_tokens":20,"completion_tokens":3}}

            data: [DONE]

            """;
        var (connector, handler, _) = Create((_, _) => StubHandler.Stream(stream));
        using var client = connector.CreateClient(Server(LlmProviderKind.OpenAiCompatible, "http://127.0.0.1:8080", "default"));

        var response = await client.GetResponseAsync(Question());

        using var body = JsonDocument.Parse(handler.Received[0].Body);
        Assert.Multiple(() =>
        {
            Assert.That(response.Text, Is.EqualTo("Bonsoir."));
            Assert.That(response.ModelId, Is.EqualTo("qwen2.5-7b"));
            Assert.That(response.Usage?.OutputTokenCount, Is.EqualTo(3));
            Assert.That(response.FinishReason, Is.EqualTo(ChatFinishReason.Stop));
            Assert.That(handler.Received[0].Request.RequestUri, Is.EqualTo(new Uri("http://127.0.0.1:8080/v1/chat/completions")));
            Assert.That(handler.Received[0].Request.Headers.Authorization, Is.Null, "A local server is called without a key when it has none.");
            Assert.That(body.RootElement.GetProperty("max_tokens").GetInt32(), Is.EqualTo(16000));
        });
    }

    [Test]
    public async Task An_openai_compatible_server_that_lists_no_model_serves_its_own()
    {
        var (connector, _, _) = Create((_, _) => StubHandler.Json("""{"data":[]}"""));

        var result = await connector.TestConnectionAsync(Server(LlmProviderKind.OpenAiCompatible, "http://127.0.0.1:8080", "anything"));

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task Openai_sends_the_key_from_the_vault()
    {
        var (connector, handler, vault) = Create((_, _) => StubHandler.Json("""{"data":[{"id":"gpt-5"}]}"""));
        await vault.SetAsync(KeyRef, new SecretValue("sk-test"));

        var result = await connector.TestConnectionAsync(Server(LlmProviderKind.OpenAi, "https://api.openai.com/v1", "gpt-5"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(handler.Received[0].Request.RequestUri, Is.EqualTo(new Uri("https://api.openai.com/v1/models")));
            Assert.That(handler.Received[0].Request.Headers.Authorization?.ToString(), Is.EqualTo("Bearer sk-test"));
        });
    }

    [Test]
    public async Task Anthropic_streams_the_messages_api_without_temperature()
    {
        const string stream = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_1","model":"claude-opus-5-5","usage":{"input_tokens":25,"output_tokens":1}}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Certainement, "}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Monsieur."}}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":6}}

            event: message_stop
            data: {"type":"message_stop"}

            """;
        var (connector, handler, vault) = Create((_, _) => StubHandler.Stream(stream));
        await vault.SetAsync(KeyRef, new SecretValue("sk-ant-test"));
        using var client = connector.CreateClient(Server(LlmProviderKind.Anthropic, "https://api.anthropic.com", "claude-opus-5-5"));

        var response = await client.GetResponseAsync(Question());

        var request = handler.Received[0].Request;
        using var body = JsonDocument.Parse(handler.Received[0].Body);
        Assert.Multiple(() =>
        {
            Assert.That(response.Text, Is.EqualTo("Certainement, Monsieur."));
            Assert.That(response.Usage?.InputTokenCount, Is.EqualTo(25));
            Assert.That(response.Usage?.OutputTokenCount, Is.EqualTo(6));
            Assert.That(response.FinishReason, Is.EqualTo(ChatFinishReason.Stop));
            Assert.That(request.RequestUri, Is.EqualTo(new Uri("https://api.anthropic.com/v1/messages")));
            Assert.That(request.Headers.GetValues("x-api-key"), Is.EqualTo(new[] { "sk-ant-test" }));
            Assert.That(request.Headers.GetValues("anthropic-version"), Is.EqualTo(new[] { "2026-10-21" }));
            Assert.That(body.RootElement.GetProperty("system").GetString(), Is.EqualTo("Vous êtes Jarvis."));
            Assert.That(body.RootElement.GetProperty("messages").GetArrayLength(), Is.EqualTo(1), "The system message goes in its own field.");
            Assert.That(body.RootElement.TryGetProperty("temperature", out _), Is.False, "The API refuses it since 2026-10-21.");
        });
    }

    [Test]
    public void The_key_is_never_in_the_failure_detail()
    {
        var (connector, _, vault) = Create((_, _) => StubHandler.Json("""{"error":{"message":"invalid x-api-key"}}""", HttpStatusCode.Unauthorized));
        vault.Secrets[KeyRef.Value] = "sk-secret-value";
        using var client = connector.CreateClient(Server(LlmProviderKind.Anthropic, "https://api.anthropic.com", "claude-opus-5-5"));

        var exception = Assert.ThrowsAsync<LlmException>(() => client.GetResponseAsync(Question()));

        Assert.Multiple(() =>
        {
            Assert.That(exception.Failure, Is.EqualTo(LlmFailure.KeyRejected));
            Assert.That(exception.Message, Does.Not.Contain("sk-secret-value"));
        });
    }
}
