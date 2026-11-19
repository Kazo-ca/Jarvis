using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>Result of one step of the state machine: the new state and the effects to publish.</summary>
/// <param name="State">The new state, equal to the previous one when the input is ignored.</param>
/// <param name="Effects">Events to publish, in order, after <c>AssistantStateChanged</c>.</param>
/// <param name="IgnoredBecause">Why the input was ignored (logged), or <see langword="null"/> when it was applied.</param>
public sealed record AssistantTransition(AssistantState State, IReadOnlyList<IEvent> Effects, string? IgnoredBecause)
{
    /// <summary>Whether the input was ignored.</summary>
    public bool IsIgnored => IgnoredBecause is not null;
}
