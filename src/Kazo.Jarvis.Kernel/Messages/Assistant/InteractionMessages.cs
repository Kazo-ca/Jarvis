using Kazo.Jarvis.Kernel.Assistant;

namespace Kazo.Jarvis.Kernel.Messages.Assistant;

/// <summary>Something starts to wait for the user: sets the condition "interaction required" (ADR 0023, section 1).</summary>
/// <param name="Id">The identifier of what waits.</param>
/// <param name="Kind">What is expected from the user.</param>
public sealed record InteractionOpened(Guid Id, InteractionKind Kind) : AssistantInput;

/// <summary>The user answered, or what waited was interrupted: clears the condition.</summary>
/// <param name="Id">The identifier of what waited.</param>
public sealed record InteractionClosed(Guid Id) : AssistantInput;
