using System.Runtime.CompilerServices;
using Kazo.Jarvis.Kernel.Llm;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="ILlmConnector"/> (ADR 0007): no network, no model. Each cause of failure is reached by
/// an address or a model, so the tests and the page can show them all:
/// a host ending in <c>.invalid</c> is unreachable, a host ending in <c>.slow</c> never answers, a cloud provider
/// whose key is the word <c>refused</c> is refused, and only the models in <see cref="Models"/> exist.
/// </summary>
public sealed class SimulatedLlmConnector : ILlmConnector
{
    private const string Answer = "Bonsoir, Monsieur. Ceci est une réponse simulée : aucun modèle n'a été appelé.";

    private readonly InMemorySecretVault vault;
    private readonly Lock gate = new();
    private readonly HashSet<string> loaded = new(StringComparer.Ordinal);

    /// <summary>Creates the simulation.</summary>
    /// <param name="vault">The simulated vault, for the keys of the cloud providers.</param>
    public SimulatedLlmConnector(InMemorySecretVault vault)
    {
        this.vault = vault;
    }

    /// <summary>The models every simulated server has.</summary>
    public static IReadOnlyList<string> Models { get; } = ["qwen3:14b", "qwen2.5:14b", "default", "claude-opus-5-5", "gpt-5"];

    /// <summary>
    /// When set, every provider fails with this cause, as if the server were stopped (<see cref="LlmFailure.Unreachable"/>)
    /// or had lost its model (<see cref="LlmFailure.ModelMissing"/>). The faults tests use it (stage1/15-faults).
    /// </summary>
    public LlmFailure? ForcedFailure { get; set; }

    /// <summary>How long a simulated load takes.</summary>
    public TimeSpan LoadDuration { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Frees every model, as Ollama does when it loads another one.</summary>
    public void Unload()
    {
        lock (gate)
        {
            loaded.Clear();
        }
    }

    /// <inheritdoc />
    public IChatClient CreateClient(LlmProvider provider) => new Client(this, provider);

    /// <inheritdoc />
    public async Task<LlmConnectionResult> TestConnectionAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        try
        {
            await CheckAsync(provider, cancellationToken).ConfigureAwait(false);
            return LlmConnectionResult.Passed(TimeSpan.FromMilliseconds(12), Models);
        }
        catch (LlmException exception)
        {
            return LlmConnectionResult.Failed(exception.Failure, exception.Message, TimeSpan.FromMilliseconds(12),
                exception.Failure == LlmFailure.ModelMissing ? Models : []);
        }
    }

    /// <inheritdoc />
    public Task<bool?> IsLoadedAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        if (provider.Kind != LlmProviderKind.Ollama)
        {
            return Task.FromResult<bool?>(null);
        }

        lock (gate)
        {
            return Task.FromResult<bool?>(loaded.Contains(provider.Model));
        }
    }

    /// <inheritdoc />
    public async Task PreloadAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        if (provider.Kind != LlmProviderKind.Ollama)
        {
            return;
        }

        await CheckAsync(provider, cancellationToken).ConfigureAwait(false);
        await Task.Delay(LoadDuration, cancellationToken).ConfigureAwait(false);
        lock (gate)
        {
            loaded.Add(provider.Model);
        }
    }

    private async Task CheckAsync(LlmProvider provider, CancellationToken cancellationToken)
    {
        if (ForcedFailure is { } forced)
        {
            throw new LlmException(forced, $"Simulated: {forced}.");
        }

        if (!provider.Endpoint.IsAbsoluteUri || (provider.Endpoint.Scheme != Uri.UriSchemeHttp && provider.Endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new LlmException(LlmFailure.InvalidAddress, "Simulated: not an http address.");
        }

        if (provider.Endpoint.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
        {
            throw new LlmException(LlmFailure.Unreachable, "Simulated: nothing answers.");
        }

        if (provider.Endpoint.Host.EndsWith(".slow", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        if (provider.Kind.RequiresKey())
        {
            var key = provider.Key is { } reference ? await vault.GetAsync(reference, cancellationToken).ConfigureAwait(false) : null;
            if (key is null)
            {
                throw new LlmException(LlmFailure.KeyMissing, "Simulated: no key.");
            }

            if (key.Reveal() == "refused")
            {
                throw new LlmException(LlmFailure.KeyRejected, "Simulated: key refused.");
            }
        }

        if (!Models.Contains(provider.Model, StringComparer.OrdinalIgnoreCase))
        {
            throw new LlmException(LlmFailure.ModelMissing, "Simulated: unknown model.");
        }
    }

    /// <summary>A chat client that says the same sentence, a word at a time.</summary>
    private sealed class Client(SimulatedLlmConnector owner, LlmProvider provider) : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            await GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken).ConfigureAwait(false);

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await owner.CheckAsync(provider, cancellationToken).ConfigureAwait(false);
            foreach (var word in Answer.Split(' '))
            {
                await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                yield return new ChatResponseUpdate(ChatRole.Assistant, word + " ") { ModelId = provider.Model };
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = 42, OutputTokenCount = 18, TotalTokenCount = 60 })])
            {
                ModelId = provider.Model,
                FinishReason = ChatFinishReason.Stop,
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceKey is null && serviceType == typeof(LlmProvider) ? provider : null;

        public void Dispose()
        {
        }
    }
}
