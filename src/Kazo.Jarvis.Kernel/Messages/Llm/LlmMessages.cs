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
