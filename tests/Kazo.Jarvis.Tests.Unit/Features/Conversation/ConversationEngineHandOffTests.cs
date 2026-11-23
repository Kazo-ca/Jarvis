using Kazo.Jarvis.Features.Conversation.Dialogue;
using Kazo.Jarvis.Features.Llm.HandOff;
using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Llm;
using Kazo.Jarvis.Kernel.Messages.Speech;
using Kazo.Jarvis.Kernel.Messages.Voice;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Tests.Unit.Kernel.Tools;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Tests.Unit.Features.Conversation;

/// <content>
/// The hand-off in a turn (US2.5 to US2.7, ADR 0020, sections 2 to 5): the local model hands over by its tool and
/// the target answers the user directly; « Demande à Claude… » goes straight to the target; the target gets the
/// history, the reading tools and no memory unless chosen; a target that fails is a cause told, never a fault.
/// </content>
public sealed partial class ConversationEngineTests
{
    private ScriptedChatClient? cloud;

    /// <summary>Sets a target of the questions and puts the tool in the registry, as the hand-off slice does.</summary>
    private void SetTarget(bool withMemory = false)
    {
        cloud = new ScriptedChatClient { ModelId = "claude-opus-5-5" };
        cloud.Pieces.AddRange(["La réponse est ", "quarante-deux. ", "Voilà."]);
        handOff.Target = new HandOffTarget(
            new LlmProvider("anthropic", "Anthropic", LlmProviderKind.Anthropic, new Uri("https://api.anthropic.com"), "claude-opus-5-5", new LlmTuning()),
            cloud,
            "Claude",
            withMemory);
        registry.Register(new FakeTool(HandOffTool.ToolName) { Origin = Kazo.Jarvis.Kernel.Tools.ToolOrigins.HandOff });
    }

    private static FunctionCallContent HandOffCall(string reason = "Cette question demande un long raisonnement.") =>
        Call(HandOffTool.ToolName, "call_h", ("kind", "question"), ("reason", reason));

    [Test]
    public async Task The_local_model_hands_over_and_the_target_answers_the_user_directly()
    {
        SetTarget();
        await journal.SaveAsync(Past("avant", "réponse d'avant", TimeSpan.FromMinutes(5)));
        memory.Recall = new MemoryRecall("[Mémoire]\n- L'utilisateur s'appelle Benoit.\n[Fin de la mémoire]", null);
        registry.Register(new FakeTool("read_file"));
        registry.Register(new FakeTool("write_note") { DeclaredClass = ActionClass.Sensitive });
        client.Rounds.Enqueue([HandOffCall()]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Explique-moi la relativité générale."));
        await TurnStoredAsync();

        var stored = journal.Turns.Last();
        var sent = cloud!.Calls.Single();
        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Has.Count.EqualTo(1), "A terminal tool: nothing goes back to the local model.");
            Assert.That(stored.Answer, Is.EqualTo("La réponse est quarante-deux. Voilà."));
            Assert.That(stored.Outcome, Is.EqualTo(TurnOutcome.Answered));
            Assert.That(stored.Provider, Is.EqualTo("Anthropic"));
            Assert.That(stored.Model, Is.EqualTo("claude-opus-5-5"));
            Assert.That((stored.InputTokens, stored.OutputTokens), Is.EqualTo((120L, 14L)), "The tokens of the target, for the spending count.");

            var entry = stored.HandOff!;
            Assert.That(entry.Kind, Is.EqualTo(HandOffKind.Question));
            Assert.That(entry.Reason, Is.EqualTo("Cette question demande un long raisonnement."));
            Assert.That(entry.Forced, Is.False);
            Assert.That(entry.Target, Is.EqualTo("Claude"));
            Assert.That((entry.FromProvider, entry.FromModel), Is.EqualTo(("Ollama", "qwen3:14b")));
            Assert.That((entry.FromInputTokens, entry.FromOutputTokens), Is.EqualTo((100L, 10L)));
            Assert.That(entry.FirstText, Is.Not.Null);
            Assert.That(entry.Failure, Is.Null);

            var call = stored.ToolCalls.Single();
            Assert.That(call.Outcome, Is.EqualTo(ToolCallOutcome.Done));
            Assert.That(call.Class, Is.EqualTo("Read"));
            Assert.That(call.Result, Is.EqualTo("Claude a répondu directement à l'utilisateur."));

            Assert.That(sent.Messages.Select(message => message.Text), Is.EqualTo(new[] { "avant", "réponse d'avant", "Explique-moi la relativité générale." }),
                "The same window of history as the local model, then the request; never the call of the local model.");
            Assert.That(sent.Options!.Instructions, Does.Contain("Règles fixes"));
            Assert.That(sent.Options.Instructions, Does.Not.Contain("[Mémoire]"), "The memory stays here unless the user chose to send it.");
            Assert.That(sent.Options.Tools!.Select(tool => tool.Name), Is.EqualTo(new[] { "read_file" }), "The reading tools only, without the hand-off tool.");
            Assert.That(client.Calls.Single().Options!.Instructions, Does.Contain("[Mémoire]"));
            Assert.That(Published<SentenceToSpeak>(), Is.Empty, "A typed request gets a written answer only.");
            Assert.That(Published<TurnFailed>(), Is.Empty);
        });
    }

    [Test]
    public async Task A_spoken_hand_over_says_the_fixed_sentence_then_the_answer_of_the_target()
    {
        SetTarget();
        client.Rounds.Enqueue(["Je vais demander plus fort que moi. ", HandOffCall()]);

        var turn = await StartSpokenTurnAsync();
        await Bus.PublishAsync(new UtteranceTranscribed(turn, "Une question difficile", AssistantLanguage.French, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(300)));
        await engine.LastAnswer.WaitAsync(Wait);

        Assert.Multiple(() =>
        {
            Assert.That(Published<SentenceToSpeak>().Select(sentence => sentence.Text),
                Is.EqualTo(new[] { "Je vais demander plus fort que moi.", "Je demande à Claude.", "La réponse est quarante-deux.", "Voilà." }));
            Assert.That(Published<AnswerSentencesEnded>(), Has.Length.EqualTo(1));
            Assert.That(journal.Turns.Single().Answer, Is.EqualTo("Je vais demander plus fort que moi. La réponse est quarante-deux. Voilà."), "The fixed sentence is said, not written in the answer.");
        });
    }

    [Test]
    public async Task The_memory_goes_to_the_target_only_when_the_user_chose_so()
    {
        SetTarget(withMemory: true);
        memory.Recall = new MemoryRecall("[Mémoire]\n- L'utilisateur s'appelle Benoit.\n[Fin de la mémoire]", null);
        client.Rounds.Enqueue([HandOffCall()]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Une question difficile"));
        await TurnStoredAsync();

        Assert.That(cloud!.Calls.Single().Options!.Instructions, Does.EndWith("[Fin de la mémoire]"));
    }

    [Test]
    public async Task Ask_Claude_goes_straight_to_the_target_without_the_local_model()
    {
        SetTarget();
        model.LoadFailure = LlmFailure.Unreachable;

        var turn = await StartSpokenTurnAsync();
        await Bus.PublishAsync(new UtteranceTranscribed(turn, "Demande à Clode, combien font six fois sept ?", AssistantLanguage.French, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(300)));
        await engine.LastAnswer.WaitAsync(Wait);
        await Bus.PublishAsync<AssistantInput>(new FirstSentenceReady(turn));
        await Bus.PublishAsync<AssistantInput>(new SpeechEnded(turn));
        await engine.LastSave.WaitAsync(Wait);

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Is.Empty, "The local model is neither loaded nor asked (US2.6).");
            Assert.That(cloud!.Calls.Single().Messages.Single().Text, Is.EqualTo("combien font six fois sept ?"), "What follows the formula.");
            Assert.That(cloud!.Calls.Single().Options!.Tools, Is.Null.Or.Empty);
            Assert.That(Published<SentenceToSpeak>().Select(sentence => sentence.Text), Is.EqualTo(new[] { "Je demande à Claude.", "La réponse est quarante-deux.", "Voilà." }));
            Assert.That(stored.Request, Is.EqualTo("Demande à Clode, combien font six fois sept ?"), "Stored as heard.");
            Assert.That(stored.Outcome, Is.EqualTo(TurnOutcome.Answered));
            Assert.That(stored.Provider, Is.EqualTo("Anthropic"));
            Assert.That(stored.HandOff!.Forced, Is.True);
            Assert.That(stored.HandOff.FromProvider, Is.Null);
            Assert.That(stored.ToolCalls, Is.Empty);
            Assert.That(handOff.Opened, Is.EqualTo(new[] { HandOffKind.Question }));
        });
    }

    [Test]
    public async Task Ask_Claude_without_a_target_says_so_and_nobody_answers_in_its_place()
    {
        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Ask Claude what a monad is."));
        await TurnStoredAsync();

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Is.Empty);
            Assert.That(stored.Answer, Does.StartWith("Le passage de main n'a pas eu lieu : aucune cible n'est réglée pour les questions."));
            Assert.That(stored.Outcome, Is.EqualTo(TurnOutcome.Answered));
            Assert.That(stored.Provider, Is.Null);
            Assert.That(stored.HandOff!.Failure, Is.EqualTo("NoQuestionsTarget"));
            Assert.That(Published<TurnFailed>(), Is.Empty, "Not a fault of the assistant.");
        });
    }

    [Test]
    public async Task Ask_Claude_Code_names_the_target_of_the_tasks_which_is_not_there_yet()
    {
        SetTarget();

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Demande à Claude Code de ranger le dépôt."));
        await TurnStoredAsync();

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(cloud!.Calls, Is.Empty, "A task never goes to the target of the questions.");
            Assert.That(client.Calls, Is.Empty);
            Assert.That(stored.Answer, Does.Contain("Claude Code, n'est pas encore disponible"));
            Assert.That(stored.HandOff!.Kind, Is.EqualTo(HandOffKind.Task));
            Assert.That(stored.HandOff.Failure, Is.EqualTo("NoTasksTarget"));
        });
    }

    [Test]
    public async Task A_target_that_fails_is_a_cause_told_to_the_local_model_which_answers()
    {
        SetTarget();
        cloud!.FailAtStart = LlmFailure.KeyRejected;
        client.Rounds.Enqueue([HandOffCall()]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Une question difficile"));
        await TurnStoredAsync();

        var stored = journal.Turns.Single();
        var call = stored.ToolCalls.Single();
        Assert.Multiple(() =>
        {
            Assert.That(call.Outcome, Is.EqualTo(ToolCallOutcome.Failed));
            Assert.That(call.Result, Is.EqualTo("L'outil a échoué : Le passage de main n'a pas eu lieu : Claude a refusé la clé."));
            Assert.That(ResultOf(client.Calls[1].Messages[^1]), Is.EqualTo(call.Result), "The cause goes back to the local model as data.");
            Assert.That(stored.Answer, Is.EqualTo("Bonsoir, Monsieur. Il est neuf heures."), "The local model answers as well as it can.");
            Assert.That(stored.Outcome, Is.EqualTo(TurnOutcome.Answered));
            Assert.That((stored.Provider, stored.Model), Is.EqualTo(("Ollama", "qwen3:14b")));
            Assert.That(stored.HandOff!.Failure, Is.EqualTo("KeyRejected"));
            Assert.That((stored.InputTokens, stored.OutputTokens), Is.EqualTo((220L, 24L)), "The tokens of the local model.");
            Assert.That(stored.HandOff.FromInputTokens, Is.Null);
            Assert.That(Published<TurnFailed>(), Is.Empty);
        });
    }

    [Test]
    public async Task A_forced_target_that_fails_is_said_to_the_user()
    {
        SetTarget();
        cloud!.FailAtStart = LlmFailure.Unreachable;

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Demande à Claude pourquoi le ciel est bleu"));
        await TurnStoredAsync();

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Is.Empty);
            Assert.That(stored.Answer, Is.EqualTo("Le passage de main n'a pas eu lieu : Claude est injoignable."));
            Assert.That(stored.Outcome, Is.EqualTo(TurnOutcome.Answered));
            Assert.That(stored.Provider, Is.Null);
            Assert.That(stored.HandOff!.Failure, Is.EqualTo("Unreachable"));
            Assert.That(Published<TurnFailed>(), Is.Empty);
        });
    }

    [Test]
    public async Task The_target_calls_a_reading_tool_through_the_decision_point_and_no_other()
    {
        SetTarget();
        var read = new FakeTool("read_file") { Run = (_, _) => ValueTask.FromResult("contenu du fichier") };
        var write = new FakeTool("write_note") { DeclaredClass = ActionClass.EverydayEffect };
        registry.Register(read);
        registry.Register(write);
        cloud!.Rounds.Enqueue([Call("read_file", "c1"), Call("write_note", "c2"), Call(HandOffTool.ToolName, "c3", ("kind", "question"), ("reason", "encore"))]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Demande à Claude de lire mes notes"));
        await TurnStoredAsync();

        var stored = journal.Turns.Single();
        var decisions = await DecisionsAsync();
        Assert.Multiple(() =>
        {
            Assert.That(stored.ToolCalls.Select(call => (call.Name, call.Outcome)), Is.EqualTo(new[]
            {
                ("read_file", ToolCallOutcome.Done),
                ("write_note", ToolCallOutcome.Denied),
                (HandOffTool.ToolName, ToolCallOutcome.Denied),
            }));
            Assert.That(read.Invoked, Has.Count.EqualTo(1));
            Assert.That(write.Invoked, Is.Empty, "A target cannot call what it was not offered.");
            Assert.That(cloud!.Calls, Has.Count.EqualTo(2), "The target never hands over again.");
            Assert.That(ResultOf(cloud!.Calls[1].Messages[^3]), Is.EqualTo("contenu du fichier"));
            Assert.That(decisions.Single().Requester, Does.Contain("anthropic"), "The decision log names the target as the requester.");
            Assert.That(stored.Answer, Is.EqualTo("La réponse est quarante-deux. Voilà."));
        });
    }

    [Test]
    public async Task An_interruption_stops_the_target()
    {
        SetTarget();
        cloud!.HoldAfterFirstPiece = new TaskCompletionSource();
        client.Rounds.Enqueue([HandOffCall()]);
        var turn = await StartSpokenTurnAsync();
        await Bus.PublishAsync(new UtteranceTranscribed(turn, "Une question difficile", AssistantLanguage.French, TimeSpan.FromSeconds(1), TimeSpan.Zero));
        await Eventually(() => journal.Turns.SingleOrDefault()?.Answer.Length > 0);

        // "Hey Jarvis" while the target answers (US1.12).
        await Bus.PublishAsync<AssistantInput>(new ActivationRequested(ActivationSource.WakeWord));
        await cloud!.Cancelled.Task.WaitAsync(Wait);
        await engine.LastSave.WaitAsync(Wait);

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(stored.Outcome, Is.EqualTo(TurnOutcome.Interrupted));
            Assert.That(stored.Answer, Is.EqualTo("La réponse est"));
            Assert.That(stored.ToolCalls.Single().Outcome, Is.EqualTo(ToolCallOutcome.Interrupted));
        });
    }

    [Test]
    public async Task Each_request_of_the_target_is_told_with_its_tokens_and_nothing_of_what_was_said()
    {
        SetTarget();
        registry.Register(new FakeTool("read_file") { Run = (_, _) => ValueTask.FromResult("contenu") });
        cloud!.Rounds.Enqueue([Call("read_file", "c1")]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Demande à Claude de lire mes notes"));
        await TurnStoredAsync();

        var answered = Published<ProviderAnswered>();
        Assert.Multiple(() =>
        {
            Assert.That(answered.Select(message => (message.Provider.Id, message.Model, message.InputTokens, message.OutputTokens)), Is.EqualTo(new[]
            {
                ("anthropic", "claude-opus-5-5", (long?)100, (long?)10),
                ("anthropic", "claude-opus-5-5", (long?)120, (long?)14),
            }), "One message per request, each with its own tokens: the cap is checked between them.");
            Assert.That(handOff.Opened, Has.Count.EqualTo(2), "Asked again before the second request.");
        });
    }

    [Test]
    public async Task The_local_model_is_never_counted()
    {
        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Quelle heure est-il ?"));
        await TurnStoredAsync();

        Assert.That(Published<ProviderAnswered>(), Is.Empty);
    }

    [Test]
    public async Task A_cap_reached_is_told_to_the_local_model_and_the_target_is_not_called()
    {
        SetTarget();
        handOff.Refusal = HandOffRefusal.CapReached;
        client.Rounds.Enqueue([HandOffCall()]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Explique-moi la relativité générale."));
        await TurnStoredAsync();

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(cloud!.Calls, Is.Empty, "At the cap the call does not happen.");
            Assert.That(ResultOf(client.Calls[1].Messages[^1]), Does.Contain("le plafond de dépense cloud de ce mois est atteint"));
            Assert.That(stored.HandOff!.Failure, Is.EqualTo("CapReached"));
            Assert.That(stored.Provider, Is.EqualTo("Ollama"), "Jarvis stays local.");
            Assert.That(stored.Outcome, Is.EqualTo(TurnOutcome.Answered));
        });
    }

    [Test]
    public async Task Ask_Claude_at_the_cap_or_without_a_price_says_so()
    {
        SetTarget();
        handOff.Refusal = HandOffRefusal.NoPrice;

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Demande à Claude la capitale de l'Australie"));
        await TurnStoredAsync();

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(cloud!.Calls, Is.Empty);
            Assert.That(client.Calls, Is.Empty, "Nobody answers in the place of the target.");
            Assert.That(stored.Answer, Does.Contain("la cible n'a pas de prix"));
            Assert.That(stored.HandOff!.Failure, Is.EqualTo("NoPrice"));
        });
    }

    [Test]
    public async Task A_cap_reached_between_two_requests_stops_the_target_there()
    {
        SetTarget();
        handOff.Refusal = HandOffRefusal.CapReached;
        handOff.AllowedOpenings = 1;
        handOff.Notices.Enqueue("Le plafond est atteint.");
        registry.Register(new FakeTool("read_file") { Run = (_, _) => ValueTask.FromResult("contenu") });
        cloud!.Rounds.Enqueue([Call("read_file", "c1")]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Demande à Claude de lire mes notes"));
        await TurnStoredAsync();

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(cloud!.Calls, Has.Count.EqualTo(1), "The second request is not made.");
            Assert.That(Published<ProviderAnswered>(), Has.Length.EqualTo(1), "What was spent is counted.");
            Assert.That(stored.Answer, Does.Contain("le plafond de dépense cloud de ce mois est atteint"));
            Assert.That(stored.Answer, Does.Not.Contain("Le plafond est atteint."), "The cause already says it: the notice is not added.");
            Assert.That(handOff.Notices, Is.Empty);
            Assert.That(stored.HandOff!.Failure, Is.EqualTo("CapReached"));
            Assert.That(stored.Provider, Is.Null, "A forced hand-off never asked the local model: nobody answered.");
        });
    }

    [Test]
    public async Task What_must_be_told_about_the_cap_is_written_and_said_after_the_answer_of_the_target()
    {
        SetTarget();
        handOff.Notices.Enqueue("Au fait, plus de 80 % du plafond est utilisé.");

        var turn = await StartSpokenTurnAsync();
        await Bus.PublishAsync(new UtteranceTranscribed(turn, "Demande à Claude combien font six fois sept", AssistantLanguage.French, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(300)));
        await engine.LastAnswer.WaitAsync(Wait);

        Assert.Multiple(() =>
        {
            Assert.That(journal.Turns.Single().Answer, Is.EqualTo("La réponse est quarante-deux. Voilà. Au fait, plus de 80 % du plafond est utilisé."));
            Assert.That(Published<SentenceToSpeak>().Select(sentence => sentence.Text),
                Is.EqualTo(new[] { "Je demande à Claude.", "La réponse est quarante-deux.", "Voilà.", "Au fait, plus de 80 % du plafond est utilisé." }));
        });
    }
}
