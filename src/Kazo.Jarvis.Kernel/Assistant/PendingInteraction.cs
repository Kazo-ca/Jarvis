namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>What Jarvis waits for from the user (ADR 0023, section 1).</summary>
public enum InteractionKind
{
    /// <summary>A question only the user can answer.</summary>
    Question,

    /// <summary>An approval asked by a session or a task.</summary>
    Approval,

    /// <summary>The confirmation of a "Sensitive" or "Critical" action (ADR 0009).</summary>
    Confirmation,

    /// <summary>Something the user must do by hand.</summary>
    ManualAction,
}

/// <summary>
/// The condition "interaction required" of the assistant state (ADR 0023, section 1): something waits for the
/// user, and stays so until the user answers. The orb shows it whatever the kind.
/// </summary>
/// <param name="Id">The identifier of what waits.</param>
/// <param name="Kind">What is expected from the user.</param>
public sealed record PendingInteraction(Guid Id, InteractionKind Kind);
