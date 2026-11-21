using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Llm;

/// <summary>
/// The loading of the default model changed (ADR 0007, section 5): published at start while it is preloaded,
/// and before a turn when Ollama had freed it.
/// </summary>
/// <param name="ProviderName">Name of the provider shown to the user.</param>
/// <param name="Model">The model.</param>
/// <param name="State">The new state.</param>
/// <param name="Failure">The cause when <paramref name="State"/> is <see cref="ModelLoadState.Failed"/>.</param>
public sealed record AssistantModelLoadChanged(string ProviderName, string Model, ModelLoadState State, LlmFailure? Failure = null) : IEvent;

/// <summary>
/// One answer of a cloud provider ended (ADR 0020, section 7): what the spending count needs, and nothing of what
/// was said. Published after each request, so the cap is checked against it before the next one.
/// </summary>
/// <param name="Provider">The provider that answered.</param>
/// <param name="Model">The model, as the server named it; <see langword="null"/> when it did not.</param>
/// <param name="InputTokens">Tokens sent, when the provider says.</param>
/// <param name="OutputTokens">Tokens written, when the provider says.</param>
/// <param name="At">When the answer ended.</param>
public sealed record ProviderAnswered(LlmProvider Provider, string? Model, long? InputTokens, long? OutputTokens, DateTimeOffset At) : IEvent;
