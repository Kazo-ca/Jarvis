using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Assistant;

/// <summary>Published after every change of the assistant state (ADR 0005, section 4).</summary>
/// <param name="Previous">The state before.</param>
/// <param name="Current">The state after.</param>
public sealed record AssistantStateChanged(AssistantState Previous, AssistantState Current) : IEvent
{
    /// <summary>The look the orb shows now.</summary>
    public AssistantLook Look => Current.Look;
}

/// <summary>How a turn was started.</summary>
public enum TurnInput
{
    /// <summary>Spoken: the microphone listens.</summary>
    Voice,

    /// <summary>Typed in the conversation window: no microphone, written answer only.</summary>
    Text,

    /// <summary>An announcement nobody asked for (ADR 0024): no microphone, no model, the text is said as it is.</summary>
    Announcement,
}

/// <summary>A new turn started. Every message of the turn carries <paramref name="Turn"/>.</summary>
/// <param name="Turn">The new turn.</param>
/// <param name="Input">Spoken or typed.</param>
/// <param name="Text">The typed text for a <see cref="TurnInput.Text"/> turn, the text to say for a <see cref="TurnInput.Announcement"/> turn, otherwise <see langword="null"/>.</param>
public sealed record TurnStarted(TurnId Turn, TurnInput Input, string? Text) : IEvent
{
    /// <summary>The thread of a subject a typed request was written in (ADR 0026, section 4), or <see langword="null"/>.</summary>
    public Kazo.Jarvis.Kernel.Conversation.ThreadAddress? Thread { get; init; }
}

/// <summary>Why a turn was cancelled.</summary>
public enum TurnCancelReason
{
    /// <summary>Hotkey or click while listening.</summary>
    ListeningCancelled,

    /// <summary>Activation while thinking or speaking (US1.12).</summary>
    Interrupted,

    /// <summary>Microphone muted while listening.</summary>
    MicrophoneMuted,

    /// <summary>Nothing heard before the timeout.</summary>
    NothingHeard,
}

/// <summary>
/// A turn was cancelled: generation stops, speech stops at once and its queue is emptied (US1.12).
/// Messages still carrying this turn are ignored by the state machine.
/// </summary>
/// <param name="Turn">The cancelled turn.</param>
/// <param name="Reason">Why.</param>
public sealed record TurnCancelled(TurnId Turn, TurnCancelReason Reason) : IEvent;

/// <summary>Why an activation was refused.</summary>
public enum ActivationRefusal
{
    /// <summary>The microphone is muted (US1.13).</summary>
    MicrophoneMuted,

    /// <summary>No usable microphone (<see cref="FaultCause.MicrophoneMissing"/>).</summary>
    MicrophoneMissing,
}

/// <summary>An activation could not start listening; the slices may show why.</summary>
/// <param name="Source">Who asked.</param>
/// <param name="Reason">Why it was refused.</param>
public sealed record ActivationRefused(ActivationSource Source, ActivationRefusal Reason) : IEvent;
