using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Permissions;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Permissions;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Assistant;

/// <summary>Every row of the transition table of ADR 0005, section 3, and the inputs it ignores.</summary>
[TestFixture]
public sealed class AssistantStateMachineTests
{
    private static readonly TurnId Turn1 = new(1);
    private static readonly TurnId Turn2 = new(2);
    private static readonly AssistantFault MicrophoneMissing = new(FaultCause.MicrophoneMissing);
    private static readonly AssistantFault ModelUnreachable = new(FaultCause.ModelUnreachable, "connection refused");

    private static readonly ActivationSource[] AllSources = Enum.GetValues<ActivationSource>();

    // Row: Idle + activation -> Listening, new turn.
    [TestCaseSource(nameof(AllSources))]
    public void Idle_activation_starts_listening_with_a_new_turn(ActivationSource source)
    {
        var transition = Apply(AssistantState.Initial, new ActivationRequested(source));

        Assert.That(transition.State.Phase, Is.EqualTo(AssistantPhase.Listening));
        Assert.That(transition.State.CurrentTurn, Is.EqualTo(Turn1));
        Assert.That(transition.Effects, Is.EqualTo(new IEvent[] { new TurnStarted(Turn1, TurnInput.Voice, null) }));
    }

    [Test]
    public void Turns_are_numbered_one_after_the_other()
    {
        var state = AssistantState.Initial;
        state = Apply(state, new ActivationRequested(ActivationSource.Hotkey)).State;
        state = Apply(state, new ActivationRequested(ActivationSource.Hotkey)).State;
        var transition = Apply(state, new ActivationRequested(ActivationSource.WakeWord));

        Assert.That(transition.State.CurrentTurn, Is.EqualTo(Turn2));
        Assert.That(transition.State.LastTurn, Is.EqualTo(Turn2));
    }

    // Row: Idle + activation, refused when the microphone is muted or missing.
    [TestCaseSource(nameof(AllSources))]
    public void Idle_activation_is_refused_when_the_microphone_is_muted(ActivationSource source)
    {
        var muted = AssistantState.Initial with { MicrophoneMuted = true };

        var transition = Apply(muted, new ActivationRequested(source));

        Assert.That(transition.State, Is.EqualTo(muted));
        Assert.That(transition.IsIgnored, Is.False);
        Assert.That(transition.Effects, Is.EqualTo(new IEvent[] { new ActivationRefused(source, ActivationRefusal.MicrophoneMuted) }));
    }

    [TestCaseSource(nameof(AllSources))]
    public void Idle_activation_is_refused_when_the_microphone_is_missing(ActivationSource source)
    {
        var missing = AssistantState.Initial with { Fault = MicrophoneMissing };

        var transition = Apply(missing, new ActivationRequested(source));

        Assert.That(transition.State, Is.EqualTo(missing));
        Assert.That(transition.Effects, Is.EqualTo(new IEvent[] { new ActivationRefused(source, ActivationRefusal.MicrophoneMissing) }));
    }

    [Test]
    public void Idle_activation_still_listens_with_a_fault_that_is_not_the_microphone()
    {
        var transition = Apply(AssistantState.Initial with { Fault = ModelUnreachable }, new ActivationRequested(ActivationSource.Click));

        Assert.That(transition.State.Phase, Is.EqualTo(AssistantPhase.Listening));
        Assert.That(transition.State.Fault, Is.EqualTo(ModelUnreachable), "the fault is cleared by a successful turn, not by an activation");
    }

    // Row: Idle + text submitted -> Thinking, new turn without the microphone.
    [Test]
    public void Idle_text_starts_thinking_without_the_microphone()
    {
        var transition = Apply(AssistantState.Initial with { MicrophoneMuted = true }, new TextSubmitted("Quelle heure est-il ?"));

        Assert.That(transition.State.Phase, Is.EqualTo(AssistantPhase.Thinking));
        Assert.That(transition.State.CurrentTurn, Is.EqualTo(Turn1));
        Assert.That(transition.Effects, Is.EqualTo(new IEvent[] { new TurnStarted(Turn1, TurnInput.Text, "Quelle heure est-il ?") }));
    }

    // Row: Listening + end of utterance -> Thinking.
    [Test]
    public void Listening_end_of_utterance_goes_to_thinking()
    {
        var transition = Apply(In(AssistantPhase.Listening), new UtteranceEnded(Turn1));

        Assert.That(transition.State.Phase, Is.EqualTo(AssistantPhase.Thinking));
        Assert.That(transition.State.CurrentTurn, Is.EqualTo(Turn1));
        Assert.That(transition.Effects, Is.Empty);
    }

    // Row: Listening + nothing heard -> Idle, turn dropped.
    [Test]
    public void Listening_nothing_heard_drops_the_turn()
    {
        var transition = Apply(In(AssistantPhase.Listening), new NothingHeard(Turn1));

        AssertIdleWithoutTurn(transition.State);
        Assert.That(transition.Effects, Is.EqualTo(new IEvent[] { new TurnCancelled(Turn1, TurnCancelReason.NothingHeard) }));
    }

    // Row: Listening + hotkey or click -> Idle, listening cancelled.
    [TestCase(ActivationSource.Hotkey)]
    [TestCase(ActivationSource.Click)]
    public void Listening_hotkey_or_click_cancels_listening(ActivationSource source)
    {
        var transition = Apply(In(AssistantPhase.Listening), new ActivationRequested(source));

        AssertIdleWithoutTurn(transition.State);
        Assert.That(transition.Effects, Is.EqualTo(new IEvent[] { new TurnCancelled(Turn1, TurnCancelReason.ListeningCancelled) }));
    }

    // "While listening, the wake word is ignored."
    [Test]
    public void Listening_wake_word_is_ignored()
    {
        var listening = In(AssistantPhase.Listening);

        var transition = Apply(listening, new ActivationRequested(ActivationSource.WakeWord));

        AssertIgnored(transition, listening);
    }

    // Row: Listening + microphone muted -> Idle, turn cancelled.
    [Test]
    public void Listening_microphone_muted_cancels_the_turn()
    {
        var transition = Apply(In(AssistantPhase.Listening), new MicrophoneMuteChanged(true));

        AssertIdleWithoutTurn(transition.State);
        Assert.That(transition.State.MicrophoneMuted, Is.True);
        Assert.That(transition.Effects, Is.EqualTo(new IEvent[] { new TurnCancelled(Turn1, TurnCancelReason.MicrophoneMuted) }));
    }

    // Row: Thinking + first sentence ready -> Speaking.
    [Test]
    public void Thinking_first_sentence_goes_to_speaking()
    {
        var transition = Apply(In(AssistantPhase.Thinking), new FirstSentenceReady(Turn1));

        Assert.That(transition.State.Phase, Is.EqualTo(AssistantPhase.Speaking));
        Assert.That(transition.State.CurrentTurn, Is.EqualTo(Turn1));
        Assert.That(transition.Effects, Is.Empty);
    }

    // Row: Thinking + answer finished without speech -> Idle.
    [Test]
    public void Thinking_answer_without_speech_goes_back_to_idle_and_clears_the_fault()
    {
        var transition = Apply(In(AssistantPhase.Thinking) with { Fault = ModelUnreachable }, new AnswerFinishedWithoutSpeech(Turn1));

        AssertIdleWithoutTurn(transition.State);
        Assert.That(transition.State.Fault, Is.Null);
        Assert.That(transition.Effects, Is.Empty);
    }

    // Row: Thinking, Speaking + activation -> Listening, interruption.
    [Test]
    public void Thinking_or_speaking_activation_interrupts_and_starts_a_new_turn(
        [Values(AssistantPhase.Thinking, AssistantPhase.Speaking)] AssistantPhase phase,
        [ValueSource(nameof(AllSources))] ActivationSource source)
    {
        var transition = Apply(In(phase), new ActivationRequested(source));

        Assert.That(transition.State.Phase, Is.EqualTo(AssistantPhase.Listening));
        Assert.That(transition.State.CurrentTurn, Is.EqualTo(Turn2));
        Assert.That(transition.Effects, Is.EqualTo(new IEvent[]
        {
            new TurnCancelled(Turn1, TurnCancelReason.Interrupted),
            new TurnStarted(Turn2, TurnInput.Voice, null),
        }));
    }

    // Not covered by ADR 0005: interruption while the microphone cannot listen. Jarvis stops, does not listen.
    [Test]
    public void Speaking_activation_with_the_microphone_muted_stops_without_listening(
        [Values(AssistantPhase.Thinking, AssistantPhase.Speaking)] AssistantPhase phase)
    {
        var transition = Apply(In(phase) with { MicrophoneMuted = true }, new ActivationRequested(ActivationSource.Click));

        AssertIdleWithoutTurn(transition.State);
        Assert.That(transition.Effects, Is.EqualTo(new IEvent[]
        {
            new TurnCancelled(Turn1, TurnCancelReason.Interrupted),
            new ActivationRefused(ActivationSource.Click, ActivationRefusal.MicrophoneMuted),
        }));
    }

    // Row: Speaking + end of speech -> Idle.
    [Test]
    public void Speaking_end_of_speech_goes_back_to_idle_and_clears_the_fault()
    {
        var transition = Apply(In(AssistantPhase.Speaking) with { Fault = ModelUnreachable }, new SpeechEnded(Turn1));

        AssertIdleWithoutTurn(transition.State);
        Assert.That(transition.State.Fault, Is.Null);
    }

    // Row: Listening, Thinking, Speaking + turn failed -> Idle with the fault.
    [Test]
    public void A_failed_turn_goes_back_to_idle_with_the_fault(
        [Values(AssistantPhase.Listening, AssistantPhase.Thinking, AssistantPhase.Speaking)] AssistantPhase phase)
    {
        var transition = Apply(In(phase), new TurnFailed(Turn1, ModelUnreachable));

        AssertIdleWithoutTurn(transition.State);
        Assert.That(transition.State.Fault, Is.EqualTo(ModelUnreachable));
        Assert.That(transition.Effects, Is.Empty);
    }

    // "A message carrying the identifier of a cancelled turn is ignored."
    [Test]
    public void A_message_from_a_cancelled_turn_is_ignored()
    {
        var state = AssistantState.Initial;
        state = Apply(state, new ActivationRequested(ActivationSource.Hotkey)).State;
        state = Apply(state, new UtteranceEnded(Turn1)).State;
        state = Apply(state, new ActivationRequested(ActivationSource.WakeWord)).State;
        Assert.That(state.CurrentTurn, Is.EqualTo(Turn2), "turn 1 was interrupted by turn 2");

        foreach (AssistantInput late in new AssistantInput[]
        {
            new UtteranceEnded(Turn1), new NothingHeard(Turn1), new FirstSentenceReady(Turn1),
            new AnswerFinishedWithoutSpeech(Turn1), new SpeechEnded(Turn1), new TurnFailed(Turn1, ModelUnreachable),
        })
        {
            AssertIgnored(Apply(state, late), state);
        }
    }

    [Test]
    public void A_turn_message_while_idle_is_ignored()
    {
        var idle = AssistantState.Initial with { LastTurn = Turn1 };

        foreach (AssistantInput late in new AssistantInput[]
        {
            new UtteranceEnded(Turn1), new NothingHeard(Turn1), new FirstSentenceReady(Turn1),
            new AnswerFinishedWithoutSpeech(Turn1), new SpeechEnded(Turn1), new TurnFailed(Turn1, ModelUnreachable),
        })
        {
            AssertIgnored(Apply(idle, late), idle);
        }
    }

    /// <summary>Every (phase, turn input) pair missing from the table is ignored.</summary>
    [TestCaseSource(nameof(MissingPairs))]
    public void A_pair_missing_from_the_table_is_ignored(AssistantPhase phase, AssistantInput input)
    {
        var state = In(phase);

        AssertIgnored(Apply(state, input), state);
    }

    [Test]
    public void A_text_is_ignored_outside_idle(
        [Values(AssistantPhase.Listening, AssistantPhase.Thinking, AssistantPhase.Speaking)] AssistantPhase phase)
    {
        var state = In(phase);

        AssertIgnored(Apply(state, new TextSubmitted("bonjour")), state);
    }

    [Test]
    public void Muting_outside_listening_only_sets_the_condition(
        [Values(AssistantPhase.Idle, AssistantPhase.Thinking, AssistantPhase.Speaking)] AssistantPhase phase)
    {
        var state = In(phase);

        var transition = Apply(state, new MicrophoneMuteChanged(true));

        Assert.That(transition.State, Is.EqualTo(state with { MicrophoneMuted = true }));
        Assert.That(transition.Effects, Is.Empty);
    }

    [Test]
    public void Unmuting_clears_the_condition_and_a_repeated_value_is_ignored()
    {
        var muted = AssistantState.Initial with { MicrophoneMuted = true };

        Assert.That(Apply(muted, new MicrophoneMuteChanged(false)).State.MicrophoneMuted, Is.False);
        AssertIgnored(Apply(muted, new MicrophoneMuteChanged(true)), muted);
        AssertIgnored(Apply(AssistantState.Initial, new MicrophoneMuteChanged(false)), AssistantState.Initial);
    }

    [Test]
    public void A_fault_raised_outside_a_turn_sets_the_condition_without_changing_the_phase(
        [Values] AssistantPhase phase)
    {
        var state = In(phase);

        var transition = Apply(state, new FaultRaised(MicrophoneMissing));

        Assert.That(transition.State, Is.EqualTo(state with { Fault = MicrophoneMissing }));
        AssertIgnored(Apply(transition.State, new FaultRaised(MicrophoneMissing)), transition.State);
    }

    [Test]
    public void A_fault_is_cleared_when_its_cause_goes_away_only()
    {
        var faulty = AssistantState.Initial with { Fault = MicrophoneMissing };

        AssertIgnored(Apply(faulty, new FaultResolved(FaultCause.ModelUnreachable)), faulty);
        Assert.That(Apply(faulty, new FaultResolved(FaultCause.MicrophoneMissing)).State.Fault, Is.Null);
        AssertIgnored(Apply(AssistantState.Initial, new FaultResolved(FaultCause.MicrophoneMissing)), AssistantState.Initial);
    }

    [Test]
    public void A_fault_with_an_origin_is_cleared_only_by_that_origin_or_by_any_origin()
    {
        var whisperMissing = AssistantState.Initial with
        {
            Fault = new AssistantFault(FaultCause.ModelMissing, "ggml-large-v3-turbo.bin", FaultOrigin.Transcription),
        };

        // A language model found again does not bring Whisper back.
        AssertIgnored(Apply(whisperMissing, new FaultResolved(FaultCause.ModelMissing, FaultOrigin.LanguageModel)), whisperMissing);
        Assert.That(Apply(whisperMissing, new FaultResolved(FaultCause.ModelMissing, FaultOrigin.Transcription)).State.Fault, Is.Null);
        Assert.That(Apply(whisperMissing, new FaultResolved(FaultCause.ModelMissing)).State.Fault, Is.Null);

        // A fault without an origin is cleared by its cause from any origin.
        var noOrigin = AssistantState.Initial with { Fault = new AssistantFault(FaultCause.ModelMissing) };
        Assert.That(Apply(noOrigin, new FaultResolved(FaultCause.ModelMissing, FaultOrigin.Voice)).State.Fault, Is.Null);
    }

    [Test]
    public void The_number_of_delegated_tasks_is_a_condition()
    {
        var speaking = In(AssistantPhase.Speaking);

        var transition = Apply(speaking, new DelegatedTaskCountChanged(3));

        Assert.That(transition.State, Is.EqualTo(speaking with { DelegatedTasks = 3 }));
        AssertIgnored(Apply(transition.State, new DelegatedTaskCountChanged(3)), transition.State);
        AssertIgnored(Apply(speaking, new DelegatedTaskCountChanged(-1)), speaking);
    }

    // ADR 0023, section 1: one more condition, and no transition of ADR 0005 changed.
    [Test]
    public void A_confirmation_request_sets_a_condition_and_leaves_the_phase_and_the_turn([Values] AssistantPhase phase)
    {
        var before = In(phase);
        var id = Guid.NewGuid();

        var opened = Apply(before, new ConfirmationOpened(id, ActionClass.Sensitive));

        Assert.That(opened.State, Is.EqualTo(before with { Confirmation = new PendingConfirmation(id, ActionClass.Sensitive) }));
        Assert.That(opened.Effects, Is.Empty);
        Assert.That(opened.State.Look, Is.EqualTo(AssistantLook.Confirmation));

        var closed = Apply(opened.State, new ConfirmationClosed(id));

        Assert.That(closed.State, Is.EqualTo(before));
        Assert.That(closed.Effects, Is.Empty);
    }

    [Test]
    public void Only_one_confirmation_request_is_open_and_only_its_own_closing_clears_it()
    {
        var id = Guid.NewGuid();
        var pending = Apply(In(AssistantPhase.Thinking), new ConfirmationOpened(id, ActionClass.Critical)).State;

        AssertIgnored(Apply(pending, new ConfirmationOpened(Guid.NewGuid(), ActionClass.Sensitive)), pending);
        AssertIgnored(Apply(pending, new ConfirmationClosed(Guid.NewGuid())), pending);
        AssertIgnored(Apply(In(AssistantPhase.Thinking), new ConfirmationClosed(id)), In(AssistantPhase.Thinking));
    }

    [Test]
    public void An_activation_during_a_confirmation_request_is_still_the_interruption()
    {
        var pending = Apply(In(AssistantPhase.Thinking), new ConfirmationOpened(Guid.NewGuid(), ActionClass.Sensitive)).State;

        var transition = Apply(pending, new ActivationRequested(ActivationSource.Click));

        // The turn is cancelled as ADR 0005 says; the confirmation slice then closes its request by a refusal.
        Assert.That(transition.Effects[0], Is.EqualTo(new TurnCancelled(Turn1, TurnCancelReason.Interrupted)));
        Assert.That(transition.State.Confirmation, Is.EqualTo(pending.Confirmation));
    }

    [Test]
    public void An_input_unknown_to_the_table_is_ignored()
    {
        AssertIgnored(Apply(AssistantState.Initial, new UnknownInput()), AssistantState.Initial);
    }

    private static IEnumerable<TestCaseData> MissingPairs()
    {
        var turnInputs = new Func<AssistantInput>[]
        {
            () => new UtteranceEnded(Turn1),
            () => new NothingHeard(Turn1),
            () => new FirstSentenceReady(Turn1),
            () => new AnswerFinishedWithoutSpeech(Turn1),
            () => new SpeechEnded(Turn1),
        };
        var allowed = new HashSet<(AssistantPhase, Type)>
        {
            (AssistantPhase.Listening, typeof(UtteranceEnded)),
            (AssistantPhase.Listening, typeof(NothingHeard)),
            (AssistantPhase.Thinking, typeof(FirstSentenceReady)),
            (AssistantPhase.Thinking, typeof(AnswerFinishedWithoutSpeech)),
            (AssistantPhase.Speaking, typeof(SpeechEnded)),
        };

        foreach (var phase in new[] { AssistantPhase.Listening, AssistantPhase.Thinking, AssistantPhase.Speaking })
        {
            foreach (var create in turnInputs)
            {
                var input = create();
                if (!allowed.Contains((phase, input.GetType())))
                {
                    yield return new TestCaseData(phase, input).SetArgDisplayNames(phase.ToString(), input.GetType().Name);
                }
            }
        }
    }

    /// <summary>A state in <paramref name="phase"/>, with turn 1 in progress unless idle.</summary>
    private static AssistantState In(AssistantPhase phase) => phase == AssistantPhase.Idle
        ? AssistantState.Initial with { LastTurn = Turn1 }
        : AssistantState.Initial with { Phase = phase, CurrentTurn = Turn1, LastTurn = Turn1 };

    private static AssistantTransition Apply(AssistantState state, AssistantInput input) => AssistantStateMachine.Apply(state, input);

    private static void AssertIdleWithoutTurn(AssistantState state)
    {
        Assert.That(state.Phase, Is.EqualTo(AssistantPhase.Idle));
        Assert.That(state.CurrentTurn, Is.EqualTo(TurnId.None));
    }

    private static void AssertIgnored(AssistantTransition transition, AssistantState unchanged)
    {
        Assert.That(transition.IsIgnored, Is.True, "the input should be ignored");
        Assert.That(transition.IgnoredBecause, Is.Not.Empty);
        Assert.That(transition.State, Is.EqualTo(unchanged));
        Assert.That(transition.Effects, Is.Empty);
    }

    private sealed record UnknownInput : AssistantInput;
}
