using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
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
        IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmTool> tools, ChatOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var model = ModelOf(options);
        var maxTokens = options?.MaxOutputTokens ?? Provider.Tuning.MaxOutputTokens;
        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["stream"] = true,
            ["stream_options"] = new { include_usage = true },
            ["messages"] = messages.Select(Write).ToArray(),
            ["temperature"] = options?.Temperature ?? Provider.Tuning.Temperature,

            // OpenAI replaced max_tokens by max_completion_tokens; local servers still read max_tokens.
            [Provider.Kind == LlmProviderKind.OpenAi ? "max_completion_tokens" : "max_tokens"] = maxTokens,
        };

        if (tools.Count > 0)
        {
            body["tools"] = tools.Select(tool => new { type = "function", function = new { name = tool.Name, description = tool.Description, parameters = tool.Schema } }).ToArray();
        }

        using var request = LlmHttp.Post(new Uri(ApiBase(Provider.Endpoint), "chat/completions"), body);
        await AuthorizeAsync(request, Provider, keys, cancellationToken).ConfigureAwait(false);
        using var response = await LlmHttp.SendAsync(Http, request, LlmHttp.Classify, cancellationToken).ConfigureAwait(false);

        string? answeredBy = null;
        ChatFinishReason? finish = null;
        long? input = null, output = null;

        // A local server may write the call in the text (ADR 0007, section 4); OpenAI's cloud never does.
        var tolerant = new TolerantToolCalls(tools, Provider.Kind != LlmProviderKind.OpenAi && Provider.Tuning.TolerantToolCalls);

        // A call arrives in pieces, by index: its id and name first, then its arguments as fragments of JSON text.
        var calls = new SortedDictionary<int, (string? Id, string Name, StringBuilder Arguments)>();
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
            if (choice.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object)
            {
                if (delta.TryGetProperty("content", out var content)
                    && content.ValueKind == JsonValueKind.String
                    && content.GetString() is { Length: > 0 } text
                    && tolerant.Push(text) is { Length: > 0 } passed)
                {
                    yield return new ChatResponseUpdate(ChatRole.Assistant, passed) { ModelId = answeredBy };
                }

                Gather(delta, calls);
            }

            if (choice.TryGetProperty("finish_reason", out var reason) && reason.ValueKind == JsonValueKind.String)
            {
                finish = reason.GetString() switch
                {
                    "stop" => ChatFinishReason.Stop,
                    "length" => ChatFinishReason.Length,
                    "content_filter" => ChatFinishReason.ContentFilter,
                    "tool_calls" or "function_call" => ChatFinishReason.ToolCalls,
                    { } other => new ChatFinishReason(other),
                    null => null,
                };
            }
        }

        foreach (var (_, call) in calls)
        {
            if (call.Name.Length > 0)
            {
                finish = ChatFinishReason.ToolCalls;
                yield return new ChatResponseUpdate(ChatRole.Assistant, [LlmHttp.Call(call.Id, call.Name, call.Arguments.ToString())]) { ModelId = answeredBy ?? model };
            }
        }

        var (written, held) = tolerant.End();
        if (written is not null)
        {
            finish = ChatFinishReason.ToolCalls;
            yield return new ChatResponseUpdate(ChatRole.Assistant, [written]) { ModelId = answeredBy ?? model };
        }
        else if (held is not null)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, held) { ModelId = answeredBy ?? model };
        }

        yield return LlmHttp.Final(answeredBy ?? model, finish, input, output);
    }

    /// <summary>Adds the pieces of tool calls of one event to <paramref name="calls"/>.</summary>
    private static void Gather(JsonElement delta, SortedDictionary<int, (string? Id, string Name, StringBuilder Arguments)> calls)
    {
        if (!delta.TryGetProperty("tool_calls", out var pieces) || pieces.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var piece in pieces.EnumerateArray())
        {
            var index = piece.TryGetProperty("index", out var position) && position.ValueKind == JsonValueKind.Number ? position.GetInt32() : calls.Count;
            var call = calls.TryGetValue(index, out var known) ? known : (Id: null, Name: string.Empty, Arguments: new StringBuilder());
            if (piece.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            {
                call.Id = id.GetString();
            }

            if (piece.TryGetProperty("function", out var function) && function.ValueKind == JsonValueKind.Object)
            {
                if (function.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                {
                    call.Name += name.GetString();
                }

                if (function.TryGetProperty("arguments", out var arguments) && arguments.ValueKind == JsonValueKind.String)
                {
                    call.Arguments.Append(arguments.GetString());
                }
            }

            calls[index] = call;
        }
    }

    /// <summary>A message in the format of <c>/v1/chat/completions</c>: the arguments of a call are JSON text, a result names its call.</summary>
    private static Dictionary<string, object?> Write(LlmMessage message)
    {
        var written = new Dictionary<string, object?> { ["role"] = message.Role, ["content"] = message.Text };
        if (message.Calls.Count > 0)
        {
            written["tool_calls"] = message.Calls
                .Select(call => new { id = call.Id, type = "function", function = new { name = call.Name, arguments = call.Arguments.GetRawText() } })
                .ToArray();
        }

        if (message.Role == "tool")
        {
            written["tool_call_id"] = message.CallId;
        }

        return written;
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
