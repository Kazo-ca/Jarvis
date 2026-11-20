using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Kernel.Llm;

/// <summary>
/// The default local model that answers at stage 1 (US1.9, ADR 0007, section 5). Given by the
/// <c>Llm/Providers</c> slice, read by the conversation slice (work item 13) without knowing it.
/// </summary>
public interface IAssistantModel
{
    /// <summary>The default provider, or <see langword="null"/> when none is configured.</summary>
    LlmProvider? Provider { get; }

    /// <summary>A client for <see cref="Provider"/>, or <see langword="null"/> when none is configured. Follows the settings.</summary>
    IChatClient? Client { get; }

    /// <summary>Where the loading of the model stands.</summary>
    ModelLoadState LoadState { get; }

    /// <summary>
    /// Makes sure the model is in memory before a turn. When Ollama freed it, publishes
    /// <see cref="Messages.Llm.AssistantModelLoadChanged"/> with <see cref="ModelLoadState.Loading"/> so the
    /// conversation says the model is loading, then waits for it. Throws <see cref="LlmException"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait (an interruption).</param>
    /// <returns>A task that completes when the model can answer.</returns>
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);
}

/// <summary>Where the loading of the default model stands.</summary>
public enum ModelLoadState
{
    /// <summary>Not known yet, or the provider cannot tell.</summary>
    Unknown,

    /// <summary>The model is being loaded; the orb stays in thinking and the conversation says so.</summary>
    Loading,

    /// <summary>The model is in memory.</summary>
    Loaded,

    /// <summary>The last load failed; the cause is in the event.</summary>
    Failed,

    /// <summary>No default provider is configured.</summary>
    NotConfigured,
}
