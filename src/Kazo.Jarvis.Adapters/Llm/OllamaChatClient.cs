using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Llm;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Adapters.Llm;

/// <summary>
/// Chat client on Ollama's own API, <c>/api/chat</c> (ADR 0007, section 2), written on <see cref="HttpClient"/> as
/// in spikes E4 and E7 to control <c>think</c>, <c>keep_alive</c> and <c>num_ctx</c>.
/// </summary>
internal sealed class OllamaChatClient : ChatClientBase
{
    /// <summary>Models that refused the <c>think</c> field: it is no longer sent to them.</summary>
    private readonly ConcurrentDictionary<string, bool> noThinking;

    public OllamaChatClient(HttpClient http, LlmProvider provider, ConcurrentDictionary<string, bool> noThinking)
        : base(http, provider, "ollama")
    {
        this.noThinking = noThinking;
    }

    /// <summary>
    /// <c>keep_alive</c> of every request: -1 keeps the model in memory while Jarvis runs (ADR 0007, section 5),
    /// otherwise Ollama's own delay applies.
    /// </summary>
    internal static object? KeepAlive(LlmProvider provider) => provider.Tuning.KeepLoaded ? -1 : null;

    /// <summary>
    /// The <c>think</c> field: always sent, off by default, so that a reasoning model such as <c>qwen3</c> answers
    /// at once (ADR 0007, section 5). A model that cannot reason refuses the field on some Ollama versions: the
    /// request is then sent again without it, and the model is remembered.
    /// </summary>
    internal static bool? Think(LlmProvider provider, ConcurrentDictionary<string, bool> noThinking, string model) =>
        noThinking.ContainsKey(model) ? null : provider.Tuning.Think;

    /// <summary>Whether an error is Ollama refusing <c>think</c> for a model that cannot reason.</summary>
    internal static bool RefusesThinking(LlmException exception) =>
        exception.Message.Contains("think", StringComparison.OrdinalIgnoreCase)
        && exception.Message.StartsWith("HTTP 400", StringComparison.Ordinal);

    /// <summary>Ollama's reading of an error: 404 on <c>/api/chat</c> is a model it does not have.</summary>
    internal static LlmFailure Classify(HttpStatusCode status, string body) =>
        status == HttpStatusCode.NotFound ? LlmFailure.ModelMissing : LlmHttp.Classify(status, body);

    /// <inheritdoc />
    protected override async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmTool> tools, ChatOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var model = ModelOf(options);
        HttpResponseMessage response;
        try
        {
            response = await SendAsync(messages, tools, options, model, Think(Provider, noThinking, model), cancellationToken).ConfigureAwait(false);
        }
        catch (LlmException exception) when (RefusesThinking(exception))
        {
            noThinking[model] = true;
            response = await SendAsync(messages, tools, options, model, null, cancellationToken).ConfigureAwait(false);
        }

        var tolerant = new TolerantToolCalls(tools, Provider.Tuning.TolerantToolCalls);
        var called = false;
        using (response)
        {
            await foreach (var line in LlmHttp.ReadLinesAsync(response, cancellationToken).ConfigureAwait(false))
            {
                using var chunk = Parse(line);
                var root = chunk.RootElement;
                if (root.TryGetProperty("error", out var error))
                {
                    throw new LlmException(LlmFailure.ServerError, $"Ollama: {error}");
                }

                var answeredBy = root.TryGetProperty("model", out var name) ? name.GetString() : model;
                if (root.TryGetProperty("message", out var message))
                {
                    if (message.TryGetProperty("content", out var content)
                        && content.GetString() is { Length: > 0 } text
                        && tolerant.Push(text) is { Length: > 0 } passed)
                    {
                        yield return new ChatResponseUpdate(ChatRole.Assistant, passed) { ModelId = answeredBy };
                    }

                    foreach (var call in Calls(message))
                    {
                        called = true;
                        yield return new ChatResponseUpdate(ChatRole.Assistant, [call]) { ModelId = answeredBy };
                    }
                }

                if (root.TryGetProperty("done", out var done) && done.GetBoolean())
                {
                    var (written, held) = tolerant.End();
                    if (written is not null)
                    {
                        called = true;
                        yield return new ChatResponseUpdate(ChatRole.Assistant, [written]) { ModelId = answeredBy };
                    }
                    else if (held is not null)
                    {
                        yield return new ChatResponseUpdate(ChatRole.Assistant, held) { ModelId = answeredBy };
                    }

                    yield return LlmHttp.Final(
                        answeredBy,
                        called ? ChatFinishReason.ToolCalls : FinishOf(root),
                        root.TryGetProperty("prompt_eval_count", out var input) ? input.GetInt64() : null,
                        root.TryGetProperty("eval_count", out var output) ? output.GetInt64() : null);
                    yield break;
                }
            }
        }

        // A stream cut before its last line: what was held back is text.
        if (tolerant.End().Text is { } rest)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, rest) { ModelId = model };
        }
    }

    /// <summary>The tool calls of a message: <c>tool_calls[].function</c>, with the arguments as an object (or as JSON text on old versions).</summary>
    private static IEnumerable<FunctionCallContent> Calls(JsonElement message)
    {
        if (!message.TryGetProperty("tool_calls", out var calls) || calls.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var call in calls.EnumerateArray())
        {
            if (!call.TryGetProperty("function", out var function)
                || !function.TryGetProperty("name", out var name)
                || name.GetString() is not { Length: > 0 } toolName)
            {
                continue;
            }

            var id = call.TryGetProperty("id", out var given) && given.ValueKind == JsonValueKind.String ? given.GetString() : null;
            if (!function.TryGetProperty("arguments", out var arguments) || arguments.ValueKind == JsonValueKind.Null)
            {
                yield return LlmHttp.Call(id, toolName, "{}");
            }
            else if (arguments.ValueKind == JsonValueKind.String)
            {
                yield return LlmHttp.Call(id, toolName, arguments.GetString() ?? string.Empty);
            }
            else
            {
                yield return LlmHttp.Call(id, toolName, arguments.Clone());
            }
        }
    }

    /// <summary>A message in Ollama's format: an assistant message carries its calls, a result names its tool.</summary>
    private static Dictionary<string, object?> Write(LlmMessage message)
    {
        var written = new Dictionary<string, object?> { ["role"] = message.Role, ["content"] = message.Text };
        if (message.Calls.Count > 0)
        {
            written["tool_calls"] = message.Calls.Select(call => new { function = new { name = call.Name, arguments = call.Arguments } }).ToArray();
        }

        if (message.Role == "tool" && message.ToolName is not null)
        {
            written["tool_name"] = message.ToolName;
        }

        return written;
    }

    private Task<HttpResponseMessage> SendAsync(
        IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmTool> tools, ChatOptions? options, string model, bool? think, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["stream"] = true,
            ["messages"] = messages.Select(Write).ToArray(),
            ["options"] = new
            {
                temperature = options?.Temperature ?? Provider.Tuning.Temperature,
                num_ctx = Provider.Tuning.ContextLength,
                num_predict = options?.MaxOutputTokens ?? Provider.Tuning.MaxOutputTokens,
            },
        };
        if (tools.Count > 0)
        {
            body["tools"] = tools.Select(tool => new { type = "function", function = new { name = tool.Name, description = tool.Description, parameters = tool.Schema } }).ToArray();
        }

        if (think is not null)
        {
            body["think"] = think;
        }

        if (KeepAlive(Provider) is { } keepAlive)
        {
            body["keep_alive"] = keepAlive;
        }

        var request = LlmHttp.Post(new Uri(LlmHttp.AsBase(Provider.Endpoint), "api/chat"), body);
        return LlmHttp.SendAsync(Http, request, Classify, cancellationToken);
    }

    private static JsonDocument Parse(string line)
    {
        try
        {
            return JsonDocument.Parse(line);
        }
        catch (JsonException exception)
        {
            throw new LlmException(LlmFailure.ServerError, "Ollama sent a line that is not JSON: is it the right kind of provider?", exception);
        }
    }

    private static ChatFinishReason? FinishOf(JsonElement root) =>
        root.TryGetProperty("done_reason", out var reason) ? reason.GetString() switch
        {
            "stop" => ChatFinishReason.Stop,
            "length" => ChatFinishReason.Length,
            { } other => new ChatFinishReason(other),
            null => null,
        }
        : ChatFinishReason.Stop;
}
