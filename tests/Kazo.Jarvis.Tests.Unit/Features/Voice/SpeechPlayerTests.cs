using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Voice.Speech;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Permissions;
using Kazo.Jarvis.Kernel.Messages.Speech;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Speech;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Features.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>
/// The answer said aloud (US1.9, US1.10, US1.12): sentences in order, first sound announced, end announced, cut
/// at once on interruption. Runs with the real kernel, so the state machine follows the phases.
/// </summary>
[TestFixture]
public sealed class SpeechPlayerTests
{
    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private FakeSpeechEngine piper = null!;
    private FakeSpeechEngine cloud = null!;
    private FakePlayback playback = null!;
    private ManualLanguage language = null!;
    private VoiceSelection selection = null!;
    private SpeechPlayer player = null!;
    private List<IEvent> published = null!;
    private List<SoundLevel> levels = null!;
    private IDisposable[] subscriptions = [];

    private IMessageBus Bus => provider.GetRequiredService<IMessageBus>();

    private IAssistantStateReader State => provider.GetRequiredService<IAssistantStateReader>();

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        piper = new FakeSpeechEngine("piper");
        piper.Add("fr_FR-tom-medium", "fr");
        piper.Add("en_GB-alan-medium", "en");
        cloud = new FakeSpeechEngine("openai", cloud: true);
        playback = new FakePlayback();
        language = new ManualLanguage { Current = AssistantLanguage.French };
        published = [];
        levels = [];

        var services = new ServiceCollection().AddJarvisKernel();
        provider = services.BuildServiceProvider();
        selection = new VoiceSelection([piper, cloud], new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System));
        player = new SpeechPlayer(selection, [piper, cloud], playback, provider.GetRequiredService<ISoundLevelSink>(), language, Bus, TimeProvider.System);
        subscriptions =
        [
            // As the slice registration does: the cancellations of the state machine reach the player.
            Bus.Subscribe<TurnCancelled>((message, token) => player.HandleAsync(message, token)),
            Bus.Subscribe<IEvent>((message, _) =>
            {
                lock (published)
                {
                    published.Add(message);
                }

                return ValueTask.CompletedTask;
            }),
            provider.GetRequiredService<ISoundLevelSource>().Subscribe(level =>
            {
                lock (levels)
                {
                    levels.Add(level);
                }
            }),
        ];
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }

        player.Dispose();
        selection.Dispose();
        provider.Dispose();
        folder.Dispose();
    }

    [Test]
    public async Task The_sentences_are_said_in_order_then_the_turn_ends()
    {
        var turn = await StartThinkingAsync();

        await SayAsync(turn, "Bonsoir, Monsieur.", "Il fait beau.", "Rien d'autre.");
        await EndAsync(turn);
        await player.LastSpeech.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(piper.Said.Select(said => said.Text), Is.EqualTo(new[] { "Bonsoir, Monsieur.", "Il fait beau.", "Rien d'autre." }));
            Assert.That(piper.Said.Select(said => said.Voice.Id), Has.All.EqualTo("fr_FR-tom-medium"));
            Assert.That(playback.Played, Has.Count.EqualTo(3));
            Assert.That(Published<FirstSentenceReady>(), Is.EqualTo(new[] { new FirstSentenceReady(turn) }));
            Assert.That(Published<SpeechEnded>(), Is.EqualTo(new[] { new SpeechEnded(turn) }));
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Idle));
            Assert.That(levels.Where(level => level.Origin == SoundOrigin.Voice).Select(level => level.Value), Has.Some.GreaterThan(0f));
            Assert.That(levels.Last(level => level.Origin == SoundOrigin.Voice).Value, Is.Zero);
        });
    }

    [Test]
    public async Task The_orb_speaks_from_the_first_sound()
    {
        playback.Hold = true;
        var turn = await StartThinkingAsync();

        await SayAsync(turn, "Bonsoir.");
        await playback.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Eventually(() => State.Current.Phase == AssistantPhase.Speaking);
        playback.StopAll();
    }

    [Test]
    public async Task The_language_of_the_assistant_and_the_chosen_speed_pick_the_voice()
    {
        language.Current = AssistantLanguage.English;
        await selection.ChooseAsync(AssistantLanguage.English, new VoiceChoice("piper", "en_GB-alan-medium", 1.4));
        var turn = await StartThinkingAsync();

        await SayAsync(turn, "Good evening.");
        await EndAsync(turn);
        await player.LastSpeech.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(piper.Said, Is.EqualTo(new[] { (piper.Voices[1], "Good evening.", 1.4) }));
    }

    [Test]
    public async Task An_interruption_cuts_the_sound_at_once_and_drops_the_queue()
    {
        playback.Hold = true;
        var turn = await StartThinkingAsync();
        await SayAsync(turn, "Première phrase.", "Deuxième phrase.", "Troisième phrase.");
        await playback.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var running = player.LastSpeech;

        // The hotkey while speaking (US1.12): the state machine cancels the turn and starts listening.
        await Bus.PublishAsync<AssistantInput>(new ActivationRequested(ActivationSource.Hotkey));
        await running.WaitAsync(TimeSpan.FromSeconds(5));
        await SayAsync(turn, "Phrase tardive.");
        await EndAsync(turn);

        Assert.Multiple(() =>
        {
            Assert.That(playback.StopAllCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(playback.Played, Has.Count.EqualTo(1));
            // The sentences after the first may have been synthesized ahead; they are never played.
            Assert.That(piper.Said.Select(said => said.Text), Does.Not.Contain("Phrase tardive."));
            Assert.That(Published<SpeechEnded>(), Is.Empty);
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Listening));
            Assert.That(player.Running, Is.Null);
            Assert.That(levels.Last(level => level.Origin == SoundOrigin.Voice).Value, Is.Zero);
        });
    }

    [Test]
    public async Task A_failure_of_the_model_in_the_middle_of_the_answer_stops_the_voice()
    {
        playback.Hold = true;
        var turn = await StartThinkingAsync();
        await SayAsync(turn, "Première phrase.", "Deuxième phrase.");
        await playback.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var running = player.LastSpeech;

        // The conversation lost the model (work item 13): the turn fails, the half-said answer stops.
        var failed = new TurnFailed(turn, new AssistantFault(FaultCause.ModelUnreachable, "Unreachable"));
        await Bus.PublishAsync<AssistantInput>(failed);
        await player.HandleAsync(failed, CancellationToken.None);
        await running.WaitAsync(TimeSpan.FromSeconds(5));
        await SayAsync(turn, "Phrase tardive.");

        Assert.Multiple(() =>
        {
            Assert.That(playback.StopAllCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(playback.Played, Has.Count.EqualTo(1));
            Assert.That(piper.Said.Select(said => said.Text), Does.Not.Contain("Phrase tardive."));
            Assert.That(Published<SpeechEnded>(), Is.Empty);
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Idle));
            Assert.That(State.Current.Fault?.Cause, Is.EqualTo(FaultCause.ModelUnreachable));
            Assert.That(player.Running, Is.Null);
        });
    }

    [Test]
    public async Task An_answer_without_sentences_ends_without_speech()
    {
        var turn = await StartThinkingAsync();

        await SayAsync(turn, "   ");
        await EndAsync(turn);
        await player.LastSpeech.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(Published<AnswerFinishedWithoutSpeech>(), Is.EqualTo(new[] { new AnswerFinishedWithoutSpeech(turn) }));
            Assert.That(playback.Played, Is.Empty);
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Idle));
        });
    }

    [Test]
    public async Task A_missing_voice_model_fails_the_turn_with_its_cause()
    {
        piper.FailOn = "phrase";
        var turn = await StartThinkingAsync();

        await SayAsync(turn, "Une phrase.");
        await EndAsync(turn);
        await player.LastSpeech.WaitAsync(TimeSpan.FromSeconds(5));

        var failed = Published<TurnFailed>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(failed.Turn, Is.EqualTo(turn));
            Assert.That(failed.Fault.Cause, Is.EqualTo(FaultCause.ModelMissing));
            Assert.That(State.Current.Fault?.Cause, Is.EqualTo(FaultCause.ModelMissing));
        });
    }

    [Test]
    public async Task No_voice_for_the_language_fails_the_turn()
    {
        piper.Voices.Clear();
        var turn = await StartThinkingAsync();

        await SayAsync(turn, "Bonsoir.");
        await player.LastSpeech.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(Published<TurnFailed>().Single().Fault.Cause, Is.EqualTo(FaultCause.ModelMissing));
    }

    [Test]
    public async Task A_cloud_refusal_fails_the_turn_as_a_missing_voice()
    {
        var nova = cloud.Add("nova", null);
        cloud.FailOn = "Bonsoir";
        cloud.Failure = SpeechFailure.KeyRefused;
        await selection.ChooseAsync(AssistantLanguage.French, nova.ToChoice());
        var turn = await StartThinkingAsync();

        await SayAsync(turn, "Bonsoir.");
        await player.LastSpeech.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(Published<TurnFailed>().Single().Fault.Cause, Is.EqualTo(FaultCause.VoiceMissing));
    }

    [Test]
    public async Task No_output_fails_the_turn_as_a_missing_voice()
    {
        playback.Unavailable = true;
        var turn = await StartThinkingAsync();

        await SayAsync(turn, "Bonsoir.");
        await player.LastSpeech.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(Published<TurnFailed>().Single().Fault.Cause, Is.EqualTo(FaultCause.VoiceMissing));
    }

    [Test]
    public async Task Sentences_of_an_older_turn_are_ignored()
    {
        var first = await StartThinkingAsync();
        await SayAsync(first, "Un.");
        await EndAsync(first);
        await player.LastSpeech.WaitAsync(TimeSpan.FromSeconds(5));

        await SayAsync(first, "Encore.");

        Assert.Multiple(() =>
        {
            Assert.That(player.Running, Is.Null);
            Assert.That(piper.Said.Select(said => said.Text), Is.EqualTo(new[] { "Un." }));
        });
    }

    /// <summary>Starts a spoken turn and ends the utterance: the state machine waits for the answer.</summary>
    // ADR 0023, section 2: the question of a confirmation request is said in the voice of the turn, and it is
    // not a sentence of the answer: the phase does not change.
    [Test]
    public async Task The_question_of_a_confirmation_request_is_said_without_entering_the_speaking_phase()
    {
        var turn = await StartThinkingAsync();
        var id = Guid.NewGuid();

        await player.HandleAsync(new ConfirmationQuestionToSay(id, turn, "Jarvis veut écrire une note. Confirmez-vous ?", true), CancellationToken.None);
        await Eventually(() => Published<ConfirmationQuestionSaid>().Length == 1);

        Assert.Multiple(() =>
        {
            Assert.That(piper.Said.Select(said => said.Text), Is.EqualTo(new[] { "Jarvis veut écrire une note. Confirmez-vous ?" }));
            Assert.That(playback.Played, Has.Count.EqualTo(1));
            Assert.That(Published<ConfirmationQuestionSaid>(), Is.EqualTo(new[] { new ConfirmationQuestionSaid(id, turn, true) }));
            Assert.That(Published<FirstSentenceReady>(), Is.Empty);
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Thinking));
        });

        // The answer of the model follows in the same turn, and only then Jarvis "speaks".
        await SayAsync(turn, "C'est noté, Monsieur.");
        await EndAsync(turn);
        await player.LastSpeech.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(Published<FirstSentenceReady>(), Is.EqualTo(new[] { new FirstSentenceReady(turn) }));
            Assert.That(Published<SpeechEnded>(), Is.EqualTo(new[] { new SpeechEnded(turn) }));
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Idle));
        });
    }

    [Test]
    public async Task A_turn_whose_only_sound_was_the_question_ends_without_speech()
    {
        var turn = await StartThinkingAsync();

        await player.HandleAsync(new ConfirmationQuestionToSay(Guid.NewGuid(), turn, "Confirmez-vous ?", false), CancellationToken.None);
        await EndAsync(turn);
        await player.LastSpeech.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(Published<ConfirmationQuestionSaid>().Single().Listen, Is.False);
            Assert.That(Published<AnswerFinishedWithoutSpeech>(), Has.Length.EqualTo(1));
            Assert.That(Published<SpeechEnded>(), Is.Empty);
        });
    }

    [Test]
    public async Task A_request_answered_while_its_question_is_said_cuts_the_question_and_not_the_turn()
    {
        var turn = await StartThinkingAsync();
        var id = Guid.NewGuid();
        playback.Hold = true;

        await player.HandleAsync(new ConfirmationQuestionToSay(id, turn, "Confirmez-vous ?", true), CancellationToken.None);
        await playback.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        playback.Hold = false;
        await player.HandleAsync(new ConfirmationClosed(id), CancellationToken.None);
        await SayAsync(turn, "C'est noté.");
        await EndAsync(turn);
        await player.LastSpeech.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(Published<ConfirmationQuestionSaid>(), Is.Empty, "Nobody listens for the answer of a closed request.");
            Assert.That(playback.Played, Has.Count.EqualTo(2), "The answer of the model is said after the cut question.");
            Assert.That(Published<SpeechEnded>(), Is.EqualTo(new[] { new SpeechEnded(turn) }));
        });
    }

    [Test]
    public async Task The_question_of_a_request_already_closed_makes_no_sound()
    {
        var turn = await StartThinkingAsync();
        var id = Guid.NewGuid();

        await player.HandleAsync(new ConfirmationClosed(id), CancellationToken.None);
        await player.HandleAsync(new ConfirmationQuestionToSay(id, turn, "Confirmez-vous ?", true), CancellationToken.None);
        await EndAsync(turn);
        await player.LastSpeech.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(playback.Played, Is.Empty);
            Assert.That(Published<ConfirmationQuestionSaid>(), Is.Empty);
        });
    }

    private async Task<TurnId> StartThinkingAsync()
    {
        await Bus.PublishAsync<AssistantInput>(new ActivationRequested(ActivationSource.Hotkey));
        var turn = State.Current.CurrentTurn;
        await Bus.PublishAsync<AssistantInput>(new UtteranceEnded(turn));
        Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Thinking));
        return turn;
    }

    private async Task SayAsync(TurnId turn, params string[] sentences)
    {
        foreach (var sentence in sentences)
        {
            await player.HandleAsync(new SentenceToSpeak(turn, sentence), CancellationToken.None);
        }
    }

    private ValueTask EndAsync(TurnId turn) => player.HandleAsync(new AnswerSentencesEnded(turn), CancellationToken.None);

    private T[] Published<T>()
    {
        lock (published)
        {
            return [.. published.OfType<T>()];
        }
    }

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The condition did not become true within 5 seconds.");
            }

            await Task.Delay(10);
        }
    }
}
