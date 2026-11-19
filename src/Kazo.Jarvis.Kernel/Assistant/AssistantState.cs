namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>
/// State of the assistant: one exclusive phase plus conditions (ADR 0005, section 1). Immutable; only the
/// state machine produces a new one.
/// </summary>
/// <param name="Phase">The exclusive phase.</param>
/// <param name="CurrentTurn">The turn in progress, or <see cref="TurnId.None"/> when idle.</param>
/// <param name="LastTurn">The last turn started, used to number the next one.</param>
/// <param name="MicrophoneMuted">Deliberate mute (US1.13).</param>
/// <param name="Fault">The fault condition, or <see langword="null"/>.</param>
/// <param name="DelegatedTasks">Number of background tasks (stage 2).</param>
public sealed record AssistantState(
    AssistantPhase Phase,
    TurnId CurrentTurn,
    TurnId LastTurn,
    bool MicrophoneMuted,
    AssistantFault? Fault,
    int DelegatedTasks)
{
    /// <summary>State at start-up: idle, no turn, no condition.</summary>
    public static AssistantState Initial { get; } = new(AssistantPhase.Idle, TurnId.None, TurnId.None, false, null, 0);

    /// <summary>Whether a turn is in progress.</summary>
    public bool HasTurn => CurrentTurn != TurnId.None;

    /// <summary>The look the orb shows for this state (ADR 0011, section 4).</summary>
    public AssistantLook Look => AssistantLooks.Of(this);
}
