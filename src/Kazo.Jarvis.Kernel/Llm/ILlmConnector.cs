using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Kernel.Llm;

/// <summary>
/// Reaches the providers (ADR 0007, section 2). The Windows host gives the hand-written adapters of
/// <c>Kazo.Jarvis.Adapters</c>, the Web host a simulation. Slices never know the protocols.
/// </summary>
public interface ILlmConnector
{
    /// <summary>
    /// A chat client for <paramref name="provider"/>, in the standard contract of <c>Microsoft.Extensions.AI</c>.
    /// Answers arrive in a stream and stop on cancellation (US1.12). A failure throws <see cref="LlmException"/>.
    /// Function invocation is never automatic: Jarvis keeps the tool loop (ADR 0007, section 2).
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>A client; dispose it when the provider changes.</returns>
    IChatClient CreateClient(LlmProvider provider);

    /// <summary>Calls the server and checks the model is there. Never throws but for cancellation.</summary>
    /// <param name="provider">The provider to test.</param>
    /// <param name="cancellationToken">Cancels the test; the caller bounds its duration.</param>
    /// <returns>The result, with the cause when it failed.</returns>
    Task<LlmConnectionResult> TestConnectionAsync(LlmProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the model is in memory now. <see langword="null"/> when the provider cannot tell (all but Ollama):
    /// the model is then taken as ready.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answer, or <see langword="null"/>.</returns>
    Task<bool?> IsLoadedAsync(LlmProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the model and keeps it in memory (ADR 0007, section 5). Does nothing for providers that cannot
    /// preload. The first load of a 9 GB model takes 70 to 115 s (spike E4). Throws <see cref="LlmException"/>.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="cancellationToken">Cancels the wait, not always the load on the server.</param>
    /// <returns>A task that completes once the model is in memory.</returns>
    Task PreloadAsync(LlmProvider provider, CancellationToken cancellationToken = default);
}
