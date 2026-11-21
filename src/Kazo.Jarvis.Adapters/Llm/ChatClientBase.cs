using System.Runtime.CompilerServices;
using Kazo.Jarvis.Kernel.Llm;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Adapters.Llm;

/// <summary>
/// Common part of the three chat clients: the full answer is the stream gathered, so every answer goes through
/// one code path (ADR 0007, section 2: every answer arrives in a stream).
/// </summary>
internal abstract class ChatClientBase : IChatClient
{
    private readonly ChatClientMetadata metadata;

    protected ChatClientBase(HttpClient http, LlmProvider provider, string providerName)
    {
        Http = http;
        Provider = provider;
        metadata = new ChatClientMetadata(providerName, provider.Endpoint, provider.Model);
    }

    protected HttpClient Http { get; }

    protected LlmProvider Provider { get; }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        await GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (!LlmHttp.IsCallable(Provider.Endpoint))
        {
            throw new LlmException(LlmFailure.InvalidAddress, $"'{Provider.Endpoint}' is not an http or https address.");
        }

        await foreach (var update in StreamAsync(LlmHttp.Flatten(messages, options), LlmHttp.Tools(options), options, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is not null ? null
            : serviceType == typeof(ChatClientMetadata) ? metadata
            : serviceType == typeof(LlmProvider) ? Provider
            : serviceType.IsInstanceOfType(this) ? this
            : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The HttpClient belongs to the connector and outlives the client.
    }

    /// <summary>
    /// Streams the answer of the provider. A tool call arrives as a <see cref="FunctionCallContent"/>; it is never
    /// run here (ADR 0007, section 2).
    /// </summary>
    protected abstract IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmTool> tools, ChatOptions? options, CancellationToken cancellationToken);

    /// <summary>The model asked for: the one of the options, else the provider's.</summary>
    protected string ModelOf(ChatOptions? options) => string.IsNullOrWhiteSpace(options?.ModelId) ? Provider.Model : options.ModelId;
}
