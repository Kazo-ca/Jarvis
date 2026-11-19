using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Assistant;

/// <summary>
/// An event the state machine reacts to (ADR 0005, section 3). Slices publish these; nobody sets the state
/// directly (section 4). The machine receives every subclass through this base type.
/// </summary>
public abstract record AssistantInput : IEvent;

/// <summary>Who asked for an activation (US1.5, US1.6).</summary>
public enum ActivationSource
{
    /// <summary>"Hey Jarvis". Ignored while listening.</summary>
    WakeWord,

    /// <summary>The global hotkey. While listening, it cancels listening.</summary>
    Hotkey,

    /// <summary>A single click on the orb. While listening, it cancels listening.</summary>
    Click,
}

/// <summary>Wake word, hotkey or click: starts listening, cancels it, or interrupts Jarvis.</summary>
/// <param name="Source">Who asked.</param>
public sealed record ActivationRequested(ActivationSource Source) : AssistantInput;

/// <summary>A text sent from the conversation window: a new turn without the microphone.</summary>
/// <param name="Text">The request as typed.</param>
public sealed record TextSubmitted(string Text) : AssistantInput;

/// <summary>The end of the utterance was detected; transcription and the model follow.</summary>
/// <param name="Turn">The turn listened to.</param>
public sealed record UtteranceEnded(TurnId Turn) : AssistantInput;

/// <summary>Nothing was heard before the timeout of the transcription slice; the turn is dropped.</summary>
/// <param name="Turn">The turn listened to.</param>
public sealed record NothingHeard(TurnId Turn) : AssistantInput;

/// <summary>The microphone was muted or unmuted (US1.13).</summary>
/// <param name="Muted">The new value.</param>
public sealed record MicrophoneMuteChanged(bool Muted) : AssistantInput;

/// <summary>The first sentence of the answer is ready to be said.</summary>
/// <param name="Turn">The turn answered.</param>
public sealed record FirstSentenceReady(TurnId Turn) : AssistantInput;

/// <summary>The answer is finished and nothing was said (a written request gets a written answer only).</summary>
/// <param name="Turn">The turn answered.</param>
public sealed record AnswerFinishedWithoutSpeech(TurnId Turn) : AssistantInput;

/// <summary>The last sentence has been said.</summary>
/// <param name="Turn">The turn answered.</param>
public sealed record SpeechEnded(TurnId Turn) : AssistantInput;

/// <summary>The turn could not finish; the fault condition is set with its cause.</summary>
/// <param name="Turn">The failed turn.</param>
/// <param name="Fault">The cause.</param>
public sealed record TurnFailed(TurnId Turn, AssistantFault Fault) : AssistantInput;

/// <summary>A fault found outside a turn (microphone unplugged while idle, unreadable configuration…).</summary>
/// <param name="Fault">The cause.</param>
public sealed record FaultRaised(AssistantFault Fault) : AssistantInput;

/// <summary>The cause of a fault went away; the condition is cleared if it is the current one.</summary>
/// <param name="Cause">The cause that went away.</param>
public sealed record FaultResolved(FaultCause Cause) : AssistantInput;

/// <summary>The number of delegated tasks changed (stage 2).</summary>
/// <param name="Count">The new number, zero or more.</param>
public sealed record DelegatedTaskCountChanged(int Count) : AssistantInput;
