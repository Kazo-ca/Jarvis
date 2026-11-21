using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>
/// The transition table of ADR 0005, section 3, as a pure function: (state, input) gives (new state,
/// effects). No input or output, no timer, no clock: a timeout is an input published by a slice.
/// </summary>
public static class AssistantStateMachine
{
    /// <summary>Applies one input to a state.</summary>
    /// <param name="state">The current state.</param>
    /// <param name="input">The event received.</param>
    /// <returns>The new state and its effects, or the same state with the reason the input is ignored.</returns>
    public static AssistantTransition Apply(AssistantState state, AssistantInput input)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(input);

        return input switch
        {
            ActivationRequested activation => Activate(state, activation.Source),
            TextSubmitted text => SubmitText(state, text),
            UtteranceEnded ended => InTurn(state, ended.Turn, AssistantPhase.Listening, () =>
                Applied(state with { Phase = AssistantPhase.Thinking })),
            NothingHeard nothing => InTurn(state, nothing.Turn, AssistantPhase.Listening, () =>
                Cancel(state, TurnCancelReason.NothingHeard)),
            FirstSentenceReady ready => InTurn(state, ready.Turn, AssistantPhase.Thinking, () =>
                Applied(state with { Phase = AssistantPhase.Speaking })),
            AnswerFinishedWithoutSpeech finished => InTurn(state, finished.Turn, AssistantPhase.Thinking, () =>
                Succeed(state)),
            SpeechEnded ended => InTurn(state, ended.Turn, AssistantPhase.Speaking, () =>
                Succeed(state)),
            TurnFailed failed => Fail(state, failed),
            MicrophoneMuteChanged mute => ChangeMute(state, mute.Muted),
            FaultRaised raised => state.Fault == raised.Fault
                ? Ignored(state, "this fault is already set")
                : Applied(state with { Fault = raised.Fault }),
            FaultResolved resolved => state.Fault?.Matches(resolved.Cause, resolved.Origin) == true
                ? Applied(state with { Fault = null })
                : Ignored(state, $"the current fault is not {resolved.Cause} from {resolved.Origin?.ToString() ?? "any origin"}"),
            DelegatedTaskCountChanged tasks => ChangeTasks(state, tasks.Count),

            // ADR 0023, section 1: a condition, like the fault; the phase and the turn do not move.
            InteractionOpened opened => state.Interaction is null
                ? Applied(state with { Interaction = new PendingInteraction(opened.Id, opened.Kind) })
                : Ignored(state, "something already waits for the user"),
            InteractionClosed closed => state.Interaction?.Id == closed.Id
                ? Applied(state with { Interaction = null })
                : Ignored(state, "this is not what waits for the user"),
            _ => Ignored(state, $"{input.GetType().Name} is not in the transition table"),
        };
    }

    private static AssistantTransition Activate(AssistantState state, ActivationSource source)
    {
        switch (state.Phase)
        {
            case AssistantPhase.Idle:
                return Refusal(state, source) is { } refusal
                    ? Applied(state, refusal)
                    : StartTurn(state, TurnInput.Voice, null);

            case AssistantPhase.Listening when source == ActivationSource.WakeWord:
                return Ignored(state, "the wake word is ignored while listening");

            case AssistantPhase.Listening:
                return Cancel(state, TurnCancelReason.ListeningCancelled);

            default:
                // Interruption (US1.12): the current turn is cancelled, speech stops, a new turn starts.
                var cancelled = new TurnCancelled(state.CurrentTurn, TurnCancelReason.Interrupted);
                var idle = state with { Phase = AssistantPhase.Idle, CurrentTurn = TurnId.None };
                if (Refusal(idle, source) is { } refused)
                {
                    // ADR 0005 does not cover an interruption with the microphone unusable: Jarvis still
                    // stops (the user asked for it), but cannot listen.
                    return Applied(idle, cancelled, refused);
                }

                var started = StartTurn(idle, TurnInput.Voice, null);
                return started with { Effects = [cancelled, .. started.Effects] };
        }
    }

    private static AssistantTransition SubmitText(AssistantState state, TextSubmitted text) =>
        state.Phase == AssistantPhase.Idle
            ? StartTurn(state, TurnInput.Text, text.Text)
            : Ignored(state, $"a text is only accepted when idle, not while {state.Phase}");

    private static AssistantTransition Fail(AssistantState state, TurnFailed failed) =>
        InTurn(state, failed.Turn, null, () => Applied(state with
        {
            Phase = AssistantPhase.Idle,
            CurrentTurn = TurnId.None,
            Fault = failed.Fault,
        }));

    private static AssistantTransition ChangeMute(AssistantState state, bool muted)
    {
        if (state.MicrophoneMuted == muted)
        {
            return Ignored(state, muted ? "the microphone is already muted" : "the microphone is not muted");
        }

        var changed = state with { MicrophoneMuted = muted };
        return muted && state.Phase == AssistantPhase.Listening
            ? Cancel(changed, TurnCancelReason.MicrophoneMuted)
            : Applied(changed);
    }

    private static AssistantTransition ChangeTasks(AssistantState state, int count)
    {
        if (count < 0)
        {
            return Ignored(state, "a number of delegated tasks cannot be negative");
        }

        return count == state.DelegatedTasks
            ? Ignored(state, "the number of delegated tasks did not change")
            : Applied(state with { DelegatedTasks = count });
    }

    private static ActivationRefused? Refusal(AssistantState state, ActivationSource source) =>
        state.MicrophoneMuted ? new ActivationRefused(source, ActivationRefusal.MicrophoneMuted)
        : state.Fault?.Cause == FaultCause.MicrophoneMissing ? new ActivationRefused(source, ActivationRefusal.MicrophoneMissing)
        : null;

    private static AssistantTransition StartTurn(AssistantState state, TurnInput input, string? text)
    {
        var turn = state.LastTurn.Next();
        var phase = input == TurnInput.Voice ? AssistantPhase.Listening : AssistantPhase.Thinking;
        return Applied(state with { Phase = phase, CurrentTurn = turn, LastTurn = turn }, new TurnStarted(turn, input, text));
    }

    private static AssistantTransition Cancel(AssistantState state, TurnCancelReason reason) =>
        Applied(
            state with { Phase = AssistantPhase.Idle, CurrentTurn = TurnId.None },
            new TurnCancelled(state.CurrentTurn, reason));

    /// <summary>A turn succeeded: back to idle, and the fault condition is cleared (ADR 0005, section 3).</summary>
    private static AssistantTransition Succeed(AssistantState state) =>
        Applied(state with { Phase = AssistantPhase.Idle, CurrentTurn = TurnId.None, Fault = null });

    /// <summary>
    /// Applies <paramref name="apply"/> only for the current turn, and, when <paramref name="phase"/> is
    /// given, only in that phase. A message from a cancelled or finished turn is ignored.
    /// </summary>
    private static AssistantTransition InTurn(AssistantState state, TurnId turn, AssistantPhase? phase, Func<AssistantTransition> apply)
    {
        if (!state.HasTurn || turn != state.CurrentTurn)
        {
            return Ignored(state, $"{turn} is not the current turn");
        }

        return phase is null || state.Phase == phase
            ? apply()
            : Ignored(state, $"expected while {phase}, received while {state.Phase}");
    }

    private static AssistantTransition Applied(AssistantState state, params IEvent[] effects) => new(state, effects, null);

    private static AssistantTransition Ignored(AssistantState state, string reason) => new(state, [], reason);
}
