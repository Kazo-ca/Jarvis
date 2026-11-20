using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Secrets;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Adapters.Llm;

/// <summary>
/// The real <see cref="ILlmConnector"/> (ADR 0007, section 2): three hand-written adapters on one
/// <see cref="HttpClient"/>, chosen by the kind of the provider.
/// </summary>
public sealed class HttpLlmConnector : ILlmConnector
{
    private readonly HttpClient http;
    private readonly LlmKeyReader keys;
    private readonly ILogger logger;
    private readonly ConcurrentDictionary<string, bool> noThinking = new(StringComparer.Ordinal);

    /// <summary>Creates the connector.</summary>
    /// <param name="http">
    /// The client used for every call. Its timeout bounds the wait for the first byte of an answer, which
    /// includes loading the model: 70 to 115 s for a 9 GB model (spike E4).
    /// </param>
    /// <param name="vault">The vault that holds the API keys; <see langword="null"/> where there is none.</param>
    /// <param name="logger">Logs the failures, never a key.</param>
    public HttpLlmConnector(HttpClient http, ISecretVault? vault, ILogger<HttpLlmConnector>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        this.http = http;
        keys = new LlmKeyReader(vault);
        this.logger = logger ?? NullLogger<HttpLlmConnector>.Instance;
    }

    /// <inheritdoc />
    public IChatClient CreateClient(LlmProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.Kind switch
        {
            LlmProviderKind.Ollama => new OllamaChatClient(http, provider, noThinking),
            LlmProviderKind.OpenAiCompatible or LlmProviderKind.OpenAi => new OpenAiChatClient(http, provider, keys),
            LlmProviderKind.Anthropic => new AnthropicChatClient(http, provider, keys),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider.Kind, "Unknown kind of provider."),
        };
    }

    /// <inheritdoc />
    public async Task<LlmConnectionResult> TestConnectionAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var watch = Stopwatch.StartNew();
        if (!LlmHttp.IsCallable(provider.Endpoint))
        {
            return LlmConnectionResult.Failed(LlmFailure.InvalidAddress, $"'{provider.Endpoint}' is not an http or https address.", watch.Elapsed);
        }

        IReadOnlyList<string> models = [];
        try
        {
            models = await ListModelsAsync(provider, cancellationToken).ConfigureAwait(false);
            if (!HasModel(provider, models))
            {
                return LlmConnectionResult.Failed(
                    LlmFailure.ModelMissing, $"'{provider.Model}' is not among the {models.Count} models of the server.", watch.Elapsed, models);
            }

            return LlmConnectionResult.Passed(watch.Elapsed, models);
        }
        catch (LlmException exception)
        {
            logger.LogInformation("Connection test of provider {Provider} failed: {Failure}, {Detail}", provider.Id, exception.Failure, exception.Message);
            return LlmConnectionResult.Failed(exception.Failure, exception.Message, watch.Elapsed, models);
        }
    }

    /// <inheritdoc />
    public async Task<bool?> IsLoadedAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (provider.Kind != LlmProviderKind.Ollama || !LlmHttp.IsCallable(provider.Endpoint))
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(LlmHttp.AsBase(provider.Endpoint), "api/ps"));
        using var response = await LlmHttp.SendAsync(http, request, LlmHttp.Classify, cancellationToken).ConfigureAwait(false);
        using var json = await LlmHttp.ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        return HasModel(provider, Names(json.RootElement, "models", "name", "model"));
    }

    /// <inheritdoc />
    public async Task PreloadAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (provider.Kind != LlmProviderKind.Ollama)
        {
            return;
        }

        if (!LlmHttp.IsCallable(provider.Endpoint))
        {
            throw new LlmException(LlmFailure.InvalidAddress, $"'{provider.Endpoint}' is not an http or https address.");
        }

        // A chat without messages loads the model and answers once it is in memory. num_ctx must be the one of
        // the chats, otherwise Ollama loads the model again at the first question.
        var body = new Dictionary<string, object?>
        {
            ["model"] = provider.Model,
            ["messages"] = Array.Empty<object>(),
            ["stream"] = false,
            ["options"] = new { num_ctx = provider.Tuning.ContextLength },
        };
        if (OllamaChatClient.KeepAlive(provider) is { } keepAlive)
        {
            body["keep_alive"] = keepAlive;
        }

        using var request = LlmHttp.Post(new Uri(LlmHttp.AsBase(provider.Endpoint), "api/chat"), body);
        using var response = await LlmHttp.SendAsync(http, request, OllamaChatClient.Classify, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether <paramref name="models"/> holds the model of <paramref name="provider"/>.</summary>
    /// <remarks>
    /// Ollama adds <c>:latest</c> to a name without a tag. An <c>openai-compatible</c> server that lists no model
    /// is taken at its word: <c>llama-server</c> serves the one it was started with, whatever the name asked.
    /// </remarks>
    internal static bool HasModel(LlmProvider provider, IReadOnlyList<string> models)
    {
        if (models.Count == 0 && provider.Kind == LlmProviderKind.OpenAiCompatible)
        {
            return true;
        }

        var wanted = provider.Model.Trim();
        return models.Any(name => string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase)
            || (provider.Kind == LlmProviderKind.Ollama && !wanted.Contains(':', StringComparison.Ordinal)
                && string.Equals(name, wanted + ":latest", StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<IReadOnlyList<string>> ListModelsAsync(LlmProvider provider, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, provider.Kind switch
        {
            LlmProviderKind.Ollama => new Uri(LlmHttp.AsBase(provider.Endpoint), "api/tags"),
            LlmProviderKind.Anthropic => new Uri(AnthropicChatClient.ApiBase(provider.Endpoint), "models?limit=1000"),
            _ => new Uri(OpenAiChatClient.ApiBase(provider.Endpoint), "models"),
        });
        switch (provider.Kind)
        {
            case LlmProviderKind.Anthropic:
                await AnthropicChatClient.AuthorizeAsync(request, provider, keys, cancellationToken).ConfigureAwait(false);
                break;
            case LlmProviderKind.OpenAi or LlmProviderKind.OpenAiCompatible:
                await OpenAiChatClient.AuthorizeAsync(request, provider, keys, cancellationToken).ConfigureAwait(false);
                break;
        }

        using var response = await LlmHttp.SendAsync(http, request, LlmHttp.Classify, cancellationToken).ConfigureAwait(false);
        using var json = await LlmHttp.ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        return provider.Kind == LlmProviderKind.Ollama
            ? Names(json.RootElement, "models", "name", "model")
            : Names(json.RootElement, "data", "id", "id");
    }

    /// <summary>The names in <c>root[array][*][property]</c>, also reading <paramref name="alternate"/>.</summary>
    private static List<string> Names(JsonElement root, string array, string property, string alternate)
    {
        var names = new List<string>();
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(array, out var items)
            && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                foreach (var key in new[] { property, alternate }.Distinct())
                {
                    if (item.TryGetProperty(key, out var value) && value.GetString() is { Length: > 0 } name && !names.Contains(name))
                    {
                        names.Add(name);
                    }
                }
            }
        }

        return names;
    }
}
