using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Adapters.Storage;
using Kazo.Jarvis.Features.Conversation.Dialogue;
using Kazo.Jarvis.Features.Permissions.Decision;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Speech;
using Kazo.Jarvis.Kernel.Messages.Voice;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Features.Settings;
using Kazo.Jarvis.Tests.Unit.Kernel.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Conversation;

/// <summary>
/// The full turn (US1.9, US1.12, US1.14, US1.15): a typed request gets a written answer only, a spoken one is said
/// sentence by sentence, an interruption stops the generation, a model failure names its cause, every turn is
/// stored with the model, the tokens and the time. Runs with the real kernel and the real SQLite store.
/// </summary>
[TestFixture]
public sealed partial class ConversationEngineTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private JsonFileSettingsStore settings = null!;
    private SqliteDocumentStore store = null!;
    private ScriptedChatClient client = null!;
    private FakeAssistantModel model = null!;
    private ManualLanguage language = null!;
    private ConversationJournal journal = null!;
    private ToolRegistry registry = null!;
    private PermissionDecisionPoint permissions = null!;
    private ConversationEngine engine = null!;
    private FakeMemory memory = null!;
    private FakeHandOff handOff = null!;
    private List<IEvent> published = null!;
    private IDisposable[] subscriptions = [];

    private IMessageBus Bus => provider.GetRequiredService<IMessageBus>();

    private IAssistantStateReader State => provider.GetRequiredService<IAssistantStateReader>();

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        provider = new ServiceCollection().AddJarvisKernel().BuildServiceProvider();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        store = new SqliteDocumentStore(folder.Paths, TimeProvider.System);
        client = new ScriptedChatClient();
        client.Pieces.AddRange(["Bonsoir, ", "Monsieur. Il est ", "neuf heures", "."]);
        model = new FakeAssistantModel(client);
        language = new ManualLanguage { Current = AssistantLanguage.French };
        journal = new ConversationJournal(store, TimeProvider.System, NullLogger<ConversationJournal>.Instance);
        registry = new ToolRegistry([], settings);
        permissions = new PermissionDecisionPoint(registry, store, TimeProvider.System, NullLogger<PermissionDecisionPoint>.Instance);
        engine = new ConversationEngine(
            model,
            new SystemPrompt(settings, TimeProvider.System),
            journal,
            language,
            registry,
            permissions,
            Bus,
            TimeProvider.System,
            NullLogger<ConversationEngine>.Instance,
            memory = new FakeMemory(),
            handOff = new FakeHandOff());
        published = [];
        subscriptions =
        [
            // As the slice registration does.
            Bus.Subscribe<TurnStarted>(engine.HandleAsync),
            Bus.Subscribe<UtteranceEnded>(engine.HandleAsync),
            Bus.Subscribe<UtteranceTranscribed>(engine.HandleAsync),
            Bus.Subscribe<FirstSentenceReady>(engine.HandleAsync),
            Bus.Subscribe<SpeechEnded>(engine.HandleAsync),
            Bus.Subscribe<AnswerFinishedWithoutSpeech>(engine.HandleAsync),
            Bus.Subscribe<TurnCancelled>(engine.HandleAsync),
            Bus.Subscribe<TurnFailed>(engine.HandleAsync),
            Bus.Subscribe<IEvent>((message, _) =>
            {
                lock (published)
                {
                    published.Add(message);
                }

                return ValueTask.CompletedTask;
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

        engine.Dispose();
        registry.Dispose();
        client.Dispose();
        cloud?.Dispose();
        provider.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        folder.Dispose();
    }

    [Test]
    public async Task A_typed_request_gets_a_written_answer_only_and_is_stored()
    {
        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("  Quelle heure est-il ?  "));
        var turn = Published<TurnStarted>().Single().Turn;
        await TurnStoredAsync();

        var shown = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(Published<SentenceToSpeak>(), Is.Empty, "Jarvis speaks only when spoken to");
            Assert.That(Published<AnswerFinishedWithoutSpeech>(), Is.EqualTo(new[] { new AnswerFinishedWithoutSpeech(turn) }));
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Idle));
            Assert.That(shown.Input, Is.EqualTo(TurnInput.Text));
            Assert.That(shown.Request, Is.EqualTo("Quelle heure est-il ?"));
            Assert.That(shown.Answer, Is.EqualTo("Bonsoir, Monsieur. Il est neuf heures."));
            Assert.That(shown.Outcome, Is.EqualTo(TurnOutcome.Answered));
            Assert.That(shown.Provider, Is.EqualTo("Ollama"));
            Assert.That(shown.Model, Is.EqualTo("qwen3:14b"));
            Assert.That(shown.InputTokens, Is.EqualTo(120));
            Assert.That(shown.OutputTokens, Is.EqualTo(14));
            Assert.That(shown.Duration, Is.Not.Null);
            Assert.That(shown.Language, Is.EqualTo("fr"));
        });

        // Read back after a restart: same turn, from the store.
        var reopened = new ConversationJournal(store, TimeProvider.System, NullLogger<ConversationJournal>.Instance);
        await reopened.EnsureLoadedAsync();
        // A record compares its list of tool calls by reference: the list is compared apart.
        var read = reopened.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(read.ToolCalls, Is.Empty);
            Assert.That(read with { ToolCalls = shown.ToolCalls }, Is.EqualTo(shown));
        });
    }

    [Test]
    public async Task A_spoken_request_is_said_sentence_by_sentence_then_the_turn_ends()
    {
        var turn = await StartSpokenTurnAsync();
        await Bus.PublishAsync(new UtteranceTranscribed(turn, "Quelle heure est-il ?", AssistantLanguage.French, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(300)));
        await engine.LastAnswer.WaitAsync(Wait);

        Assert.Multiple(() =>
        {
            Assert.That(Published<SentenceToSpeak>().Select(sentence => sentence.Text), Is.EqualTo(new[] { "Bonsoir, Monsieur.", "Il est neuf heures." }));
            Assert.That(Published<SentenceToSpeak>().Select(sentence => sentence.Turn), Has.All.EqualTo(turn));
            Assert.That(Published<AnswerSentencesEnded>(), Is.EqualTo(new[] { new AnswerSentencesEnded(turn) }));
            Assert.That(journal.Turns.Single().Outcome, Is.EqualTo(TurnOutcome.Pending), "the voice still speaks");
        });

        // What the voice slice does: first sound, then the end of the speech.
        await Bus.PublishAsync<AssistantInput>(new FirstSentenceReady(turn));
        Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Speaking));
        await Bus.PublishAsync<AssistantInput>(new SpeechEnded(turn));
        await engine.LastSave.WaitAsync(Wait);

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Idle));
            Assert.That(stored.Input, Is.EqualTo(TurnInput.Voice));
            Assert.That(stored.Outcome, Is.EqualTo(TurnOutcome.Answered));
            Assert.That(stored.FirstSound, Is.Not.Null);
        });
    }

    [Test]
    public async Task The_system_message_holds_the_personality_the_rules_and_the_history()
    {
        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Première question"));
        await TurnStoredAsync();
        await settings.WriteAsync(ConversationOptions.Section, new ConversationOptions().WithPersonality(AssistantLanguage.French, "Tu es un majordome très bref."));

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Deuxième question"));
        await TurnStoredAsync();

        var (first, second) = (client.Calls[0], client.Calls[1]);
        Assert.Multiple(() =>
        {
            Assert.That(first.Options?.Instructions, Does.StartWith(SystemPrompt.DefaultPersonality(AssistantLanguage.French)));
            Assert.That(first.Options?.Instructions, Does.Contain("réponds en français"));
            Assert.That(first.Messages.Select(message => message.Text), Is.EqualTo(new[] { "Première question" }));
            Assert.That(second.Options?.Instructions, Does.StartWith("Tu es un majordome très bref."));
            Assert.That(second.Messages.Select(message => (message.Role, message.Text)), Is.EqualTo(new[]
            {
                (ChatRole.User, "Première question"),
                (ChatRole.Assistant, "Bonsoir, Monsieur. Il est neuf heures."),
                (ChatRole.User, "Deuxième question"),
            }));
        });
    }

    [Test]
    public async Task An_interruption_stops_the_generation_and_listens_again()
    {
        client.HoldAfterFirstPiece = new TaskCompletionSource();
        var turn = await StartSpokenTurnAsync();
        await Bus.PublishAsync(new UtteranceTranscribed(turn, "Raconte une histoire.", AssistantLanguage.French, TimeSpan.FromSeconds(1), TimeSpan.Zero));
        await Eventually(() => journal.Turns.SingleOrDefault()?.Answer == "Bonsoir, ");

        // "Hey Jarvis" while Jarvis thinks (US1.12).
        await Bus.PublishAsync<AssistantInput>(new ActivationRequested(ActivationSource.WakeWord));
        await client.Cancelled.Task.WaitAsync(Wait);
        await engine.LastSave.WaitAsync(Wait);

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Listening));
            Assert.That(State.Current.CurrentTurn, Is.Not.EqualTo(turn));
            Assert.That(stored.Outcome, Is.EqualTo(TurnOutcome.Interrupted));
            Assert.That(stored.Answer, Is.EqualTo("Bonsoir,"));
            Assert.That(Published<AnswerSentencesEnded>(), Is.Empty);
            Assert.That(Published<TurnFailed>(), Is.Empty);
        });
    }

    [TestCase(LlmFailure.Unreachable, FaultCause.ModelUnreachable)]
    [TestCase(LlmFailure.ModelMissing, FaultCause.ModelMissing)]
    [TestCase(LlmFailure.NotConfigured, FaultCause.ModelUnreachable)]
    public async Task A_model_that_does_not_answer_fails_the_turn_with_its_cause(LlmFailure failure, FaultCause cause)
    {
        model.LoadFailure = failure;

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Bonjour"));
        await TurnStoredAsync();

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Idle));
            Assert.That(State.Current.Fault, Is.EqualTo(new AssistantFault(cause, failure.ToString(), FaultOrigin.LanguageModel)));
            Assert.That(stored.Outcome, Is.EqualTo(TurnOutcome.Failed));
            Assert.That(stored.Failure, Is.EqualTo(failure.ToString()));
        });
    }

    [Test]
    public async Task A_failure_in_the_middle_of_a_spoken_answer_ends_the_turn_without_the_end_of_sentences()
    {
        client.FailAtEnd = LlmFailure.TimedOut;
        var turn = await StartSpokenTurnAsync();
        await Bus.PublishAsync(new UtteranceTranscribed(turn, "Bonjour", AssistantLanguage.French, TimeSpan.FromSeconds(1), TimeSpan.Zero));
        await TurnStoredAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Published<SentenceToSpeak>(), Is.Not.Empty, "the sentences before the failure were sent");
            Assert.That(Published<AnswerSentencesEnded>(), Is.Empty);
            Assert.That(Published<TurnFailed>().Single().Turn, Is.EqualTo(turn));
            Assert.That(State.Current.Fault?.Cause, Is.EqualTo(FaultCause.ModelUnreachable));
            Assert.That(journal.Turns.Single().Failure, Is.EqualTo("TimedOut"));
        });
    }

    [Test]
    public async Task Nothing_heard_by_Whisper_ends_the_turn_without_an_answer()
    {
        var turn = await StartSpokenTurnAsync();

        await Bus.PublishAsync(new UtteranceTranscribed(turn, string.Empty, AssistantLanguage.French, TimeSpan.FromSeconds(1), TimeSpan.Zero));

        Assert.Multiple(() =>
        {
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Idle));
            Assert.That(client.Calls, Is.Empty);
            Assert.That(journal.Turns, Is.Empty);
        });
    }

    [Test]
    public void The_history_keeps_the_newest_answered_turns_that_fit()
    {
        var turns = new[]
        {
            Turn("ancienne", "réponse ancienne", TurnOutcome.Answered),
            Turn("interrompue", "moitié", TurnOutcome.Interrupted),
            Turn("échouée", string.Empty, TurnOutcome.Failed),
            Turn("récente", "réponse récente", TurnOutcome.Answered),
        };

        var all = ConversationEngine.History(turns, 4096);
        var small = ConversationEngine.History(turns, 16);

        Assert.Multiple(() =>
        {
            Assert.That(all.Select(message => message.Text), Is.EqualTo(new[] { "ancienne", "réponse ancienne", "récente", "réponse récente" }));
            Assert.That(small.Select(message => message.Text), Is.EqualTo(new[] { "récente", "réponse récente" }));
            Assert.That(ConversationEngine.History(turns, 0), Is.Empty);
        });
    }

    [Test]
    public async Task Erasing_everything_empties_the_journal_and_the_store()
    {
        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Bonjour"));
        await TurnStoredAsync();

        await journal.EraseAllAsync();

        var reopened = new ConversationJournal(store, TimeProvider.System, NullLogger<ConversationJournal>.Instance);
        await reopened.EnsureLoadedAsync();
        Assert.Multiple(() =>
        {
            Assert.That(journal.Turns, Is.Empty);
            Assert.That(reopened.Turns, Is.Empty);
        });
    }

    private static ConversationTurn Turn(string request, string answer, TurnOutcome outcome) =>
        new() { Request = request, Answer = answer, Outcome = outcome, At = DateTimeOffset.UtcNow };

    /// <summary>The answer ends the turn before its task completes; the storing starts there.</summary>
    private async Task TurnStoredAsync()
    {
        await engine.LastAnswer.WaitAsync(Wait);
        await engine.LastSave.WaitAsync(Wait);
    }

    private async Task<TurnId> StartSpokenTurnAsync()
    {
        await Bus.PublishAsync<AssistantInput>(new ActivationRequested(ActivationSource.Hotkey));
        var turn = State.Current.CurrentTurn;
        await Bus.PublishAsync<AssistantInput>(new UtteranceEnded(turn));
        Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Thinking));
        return turn;
    }

    private T[] Published<T>()
    {
        lock (published)
        {
            return [.. published.OfType<T>()];
        }
    }

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.Add(Wait);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The condition was not met in time.");
            }

            await Task.Delay(10);
        }
    }
}
