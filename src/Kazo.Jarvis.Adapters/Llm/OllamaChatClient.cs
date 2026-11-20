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
        IReadOnlyList<(string Role, string Text)> messages, ChatOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var model = ModelOf(options);
        HttpResponseMessage response;
        try
        {
            response = await SendAsync(messages, options, model, Think(Provider, noThinking, model), cancellationToken).ConfigureAwait(false);
        }
        catch (LlmException exception) when (RefusesThinking(exception))
        {
            noThinking[model] = true;
            response = await SendAsync(messages, options, model, null, cancellationToken).ConfigureAwait(false);
        }

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
                if (root.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var content)
                    && content.GetString() is { Length: > 0 } text)
                {
                    yield return new ChatResponseUpdate(ChatRole.Assistant, text) { ModelId = answeredBy };
                }

                if (root.TryGetProperty("done", out var done) && done.GetBoolean())
                {
                    yield return LlmHttp.Final(
                        answeredBy,
                        FinishOf(root),
                        root.TryGetProperty("prompt_eval_count", out var input) ? input.GetInt64() : null,
                        root.TryGetProperty("eval_count", out var output) ? output.GetInt64() : null);
                    yield break;
                }
            }
        }
    }

    private Task<HttpResponseMessage> SendAsync(
        IReadOnlyList<(string Role, string Text)> messages, ChatOptions? options, string model, bool? think, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["stream"] = true,
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Text }).ToArray(),
            ["options"] = new
            {
                temperature = options?.Temperature ?? Provider.Tuning.Temperature,
                num_ctx = Provider.Tuning.ContextLength,
                num_predict = options?.MaxOutputTokens ?? Provider.Tuning.MaxOutputTokens,
            },
        };
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
