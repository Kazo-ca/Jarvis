using Kazo.Jarvis.Kernel.Localization;

namespace Kazo.Jarvis.Kernel.Conversation;

/// <summary>
/// What must be recalled to the model at the start of a turn (ADR 0019, section 7). The conversation slice asks;
/// the memory slice answers. Without an implementation, the conversation works as before the memory existed.
/// </summary>
public interface IConversationMemory
{
    /// <summary>What the model is reminded of in this turn.</summary>
    /// <param name="language">The language of the turn.</param>
    /// <param name="contextLength">The context of the model that answers, in tokens: the memory takes a fixed share of it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The recall; <see cref="MemoryRecall.Nothing"/> when the memory is off or empty.</returns>
    ValueTask<MemoryRecall> RecallAsync(AssistantLanguage language, int contextLength, CancellationToken cancellationToken = default);

    /// <summary>
    /// What one agent kept for its own work (ADR 0025, section 5): sent only when this agent answers, after the
    /// block of <see cref="RecallAsync"/>.
    /// </summary>
    /// <param name="agent">The identity of the agent that answers.</param>
    /// <param name="language">The language of the turn.</param>
    /// <param name="contextLength">The context of the model that answers, in tokens.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A delimited block that says of itself that it is data, or <see langword="null"/> when there is nothing.</returns>
    ValueTask<string?> RecallForAgentAsync(string agent, AssistantLanguage language, int contextLength, CancellationToken cancellationToken = default) => new((string?)null);
}

/// <summary>What the memory gives to a turn.</summary>
/// <param name="Block">
/// The text added to the system message after the fixed rules: a delimited block that says of itself that it is
/// data. <see langword="null"/> when there is nothing to recall.
/// </param>
/// <param name="OldAfter">
/// The silence after which the exchanges before it are no longer sent word for word (ADR 0019, section 5).
/// <see langword="null"/> when age does not count.
/// </param>
public sealed record MemoryRecall(string? Block, TimeSpan? OldAfter)
{
    /// <summary>Nothing to recall, and no limit of age.</summary>
    public static MemoryRecall Nothing { get; } = new(null, null);
}
