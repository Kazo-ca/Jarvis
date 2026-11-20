using System.Runtime.CompilerServices;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Llm;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Adapters.Llm;

/// <summary>
/// Chat client on Anthropic's Messages API (ADR 0007, section 2), written on <see cref="HttpClient"/> like the two
/// others. Cloud providers are configured and tested at stage 1; they answer from stage 2 (hand-off).
/// </summary>
internal sealed class AnthropicChatClient : ChatClientBase
{
    /// <summary>Version of the API sent in <c>anthropic-version</c>. It no longer accepts <c>temperature</c>.</summary>
    internal const string ApiVersion = "2026-10-21";

    private readonly LlmKeyReader keys;

    public AnthropicChatClient(HttpClient http, LlmProvider provider, LlmKeyReader keys)
        : base(http, provider, "anthropic")
    {
        this.keys = keys;
    }

    /// <summary>The base of the API: the configured address, with <c>v1/</c> added unless it already ends with it.</summary>
    internal static Uri ApiBase(Uri endpoint)
    {
        var root = LlmHttp.AsBase(endpoint);
        return root.AbsolutePath.EndsWith("/v1/", StringComparison.Ordinal) ? root : new Uri(root, "v1/");
    }

    /// <summary>Adds the version and the key. A provider without a key fails here.</summary>
    internal static async Task AuthorizeAsync(HttpRequestMessage request, LlmProvider provider, LlmKeyReader keys, CancellationToken cancellationToken)
    {
        request.Headers.Add("anthropic-version", ApiVersion);
        var key = await keys.ReadAsync(provider, cancellationToken).ConfigureAwait(false);
        if (key is not null)
        {
            request.Headers.Add("x-api-key", key);
        }
    }

    /// <inheritdoc />
    protected override async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IReadOnlyList<(string Role, string Text)> messages, ChatOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var model = ModelOf(options);
        var system = string.Join("\n\n", messages.Where(m => m.Role == "system").Select(m => m.Text));
        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["stream"] = true,
            ["max_tokens"] = options?.MaxOutputTokens ?? Provider.Tuning.MaxOutputTokens,
            ["messages"] = messages.Where(m => m.Role != "system").Select(m => new { role = m.Role, content = m.Text }).ToArray(),
        };
        if (system.Length > 0)
        {
            body["system"] = system;
        }

        using var request = LlmHttp.Post(new Uri(ApiBase(Provider.Endpoint), "messages"), body);
        await AuthorizeAsync(request, Provider, keys, cancellationToken).ConfigureAwait(false);
        using var response = await LlmHttp.SendAsync(Http, request, LlmHttp.Classify, cancellationToken).ConfigureAwait(false);

        string? answeredBy = model;
        ChatFinishReason? finish = null;
        long? input = null, output = null;
        await foreach (var line in LlmHttp.ReadLinesAsync(response, cancellationToken).ConfigureAwait(false))
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            using var chunk = Parse(line[5..].Trim());
            var root = chunk.RootElement;
            switch (root.TryGetProperty("type", out var type) ? type.GetString() : null)
            {
                case "message_start" when root.TryGetProperty("message", out var message):
                    answeredBy = message.TryGetProperty("model", out var name) ? name.GetString() : answeredBy;
                    if (message.TryGetProperty("usage", out var startUsage))
                    {
                        input = startUsage.TryGetProperty("input_tokens", out var i) ? i.GetInt64() : input;
                    }

                    break;
                case "content_block_delta" when root.TryGetProperty("delta", out var delta)
                    && delta.TryGetProperty("text", out var text)
                    && text.GetString() is { Length: > 0 } piece:
                    yield return new ChatResponseUpdate(ChatRole.Assistant, piece) { ModelId = answeredBy };
                    break;
                case "message_delta":
                    if (root.TryGetProperty("delta", out var end) && end.TryGetProperty("stop_reason", out var reason))
                    {
                        finish = reason.GetString() switch
                        {
                            "end_turn" or "stop_sequence" => ChatFinishReason.Stop,
                            "max_tokens" => ChatFinishReason.Length,
                            "refusal" => ChatFinishReason.ContentFilter,
                            { } other => new ChatFinishReason(other),
                            null => null,
                        };
                    }

                    if (root.TryGetProperty("usage", out var endUsage))
                    {
                        output = endUsage.TryGetProperty("output_tokens", out var o) ? o.GetInt64() : output;
                    }

                    break;
                case "error":
                    throw new LlmException(LlmFailure.ServerError, $"Anthropic error in the stream: {root}");
                case "message_stop":
                    yield return LlmHttp.Final(answeredBy, finish, input, output);
                    yield break;
            }
        }

        yield return LlmHttp.Final(answeredBy, finish, input, output);
    }

    private static JsonDocument Parse(string data)
    {
        try
        {
            return JsonDocument.Parse(data);
        }
        catch (JsonException exception)
        {
            throw new LlmException(LlmFailure.ServerError, "Anthropic sent an event that is not JSON.", exception);
        }
    }
}
