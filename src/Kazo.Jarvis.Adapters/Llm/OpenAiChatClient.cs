using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Llm;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Adapters.Llm;

/// <summary>
/// Chat client on <c>/v1/chat/completions</c> (ADR 0007, section 2): one adapter for <c>openai-compatible</c>
/// servers (<c>llama-server</c>, LM Studio) and for OpenAI's cloud API. Answers arrive as server-sent events.
/// </summary>
internal sealed class OpenAiChatClient : ChatClientBase
{
    private readonly LlmKeyReader keys;

    public OpenAiChatClient(HttpClient http, LlmProvider provider, LlmKeyReader keys)
        : base(http, provider, provider.Kind == LlmProviderKind.OpenAi ? "openai" : "openai-compatible")
    {
        this.keys = keys;
    }

    /// <summary>
    /// The base of the API: the configured address, with <c>v1/</c> added when it has no path, so that
    /// <c>http://127.0.0.1:8080</c> and <c>https://api.openai.com/v1</c> both work.
    /// </summary>
    internal static Uri ApiBase(Uri endpoint)
    {
        var root = LlmHttp.AsBase(endpoint);
        return root.AbsolutePath == "/" ? new Uri(root, "v1/") : root;
    }

    /// <summary>Adds the key, when the provider has one, as a bearer token. A cloud provider without a key fails here.</summary>
    internal static async Task AuthorizeAsync(HttpRequestMessage request, LlmProvider provider, LlmKeyReader keys, CancellationToken cancellationToken)
    {
        var key = await keys.ReadAsync(provider, cancellationToken).ConfigureAwait(false);
        if (key is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
    }

    /// <inheritdoc />
    protected override async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IReadOnlyList<(string Role, string Text)> messages, ChatOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var model = ModelOf(options);
        var maxTokens = options?.MaxOutputTokens ?? Provider.Tuning.MaxOutputTokens;
        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["stream"] = true,
            ["stream_options"] = new { include_usage = true },
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Text }).ToArray(),
            ["temperature"] = options?.Temperature ?? Provider.Tuning.Temperature,

            // OpenAI replaced max_tokens by max_completion_tokens; local servers still read max_tokens.
            [Provider.Kind == LlmProviderKind.OpenAi ? "max_completion_tokens" : "max_tokens"] = maxTokens,
        };

        using var request = LlmHttp.Post(new Uri(ApiBase(Provider.Endpoint), "chat/completions"), body);
        await AuthorizeAsync(request, Provider, keys, cancellationToken).ConfigureAwait(false);
        using var response = await LlmHttp.SendAsync(Http, request, LlmHttp.Classify, cancellationToken).ConfigureAwait(false);

        string? answeredBy = null;
        ChatFinishReason? finish = null;
        long? input = null, output = null;
        await foreach (var line in LlmHttp.ReadLinesAsync(response, cancellationToken).ConfigureAwait(false))
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line[5..].Trim();
            if (data == "[DONE]")
            {
                break;
            }

            using var chunk = Parse(data);
            var root = chunk.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                throw new LlmException(LlmFailure.ServerError, $"Server error in the stream: {error}");
            }

            answeredBy = root.TryGetProperty("model", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : answeredBy ?? model;
            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                input = usage.TryGetProperty("prompt_tokens", out var p) ? p.GetInt64() : input;
                output = usage.TryGetProperty("completion_tokens", out var c) ? c.GetInt64() : output;
            }

            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                continue;
            }

            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String
                && content.GetString() is { Length: > 0 } text)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, text) { ModelId = answeredBy };
            }

            if (choice.TryGetProperty("finish_reason", out var reason) && reason.ValueKind == JsonValueKind.String)
            {
                finish = reason.GetString() switch
                {
                    "stop" => ChatFinishReason.Stop,
                    "length" => ChatFinishReason.Length,
                    "content_filter" => ChatFinishReason.ContentFilter,
                    { } other => new ChatFinishReason(other),
                    null => null,
                };
            }
        }

        yield return LlmHttp.Final(answeredBy ?? model, finish, input, output);
    }

    private static JsonDocument Parse(string data)
    {
        try
        {
            return JsonDocument.Parse(data);
        }
        catch (JsonException exception)
        {
            throw new LlmException(LlmFailure.ServerError, "The server sent an event that is not JSON: is it the right kind of provider?", exception);
        }
    }
}
