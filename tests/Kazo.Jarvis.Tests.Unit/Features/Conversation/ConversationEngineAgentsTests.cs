using Kazo.Jarvis.Features.Conversation.Dialogue;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Speech;
using Kazo.Jarvis.Kernel.Messages.Voice;
using Kazo.Jarvis.Tests.Unit.Kernel.Tools;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Tests.Unit.Features.Conversation;

/// <content>
/// The agents in a turn (ADR 0025, sections 4, 8 and 9): the Jarvis agent receives the request with its own tools
/// and answers a simple one in a single call; it hands the rest over, and the turn continues with the agent named,
/// its instructions and its tools; an agent gives back what is not for it; the user may choose the agent of a typed
/// request. Runs with the real catalog of the agents over files in memory.
/// </content>
public sealed partial class ConversationEngineTests
{
    private const string WithAgents = "Agents";

    private static string[] Offered((IReadOnlyList<ChatMessage> Messages, ChatOptions? Options) call) =>
        [.. (call.Options?.Tools ?? []).Select(tool => tool.Name)];

    private (FakeTool Clock, FakeTool Search) RegisterClockAndSearch()
    {
        var clock = new FakeTool("get_datetime");
        var search = new FakeTool("web_search") { Run = (_, _) => ValueTask.FromResult("Paris : 12 °C.") };
        registry.Register(clock);
        registry.Register(search);
        return (clock, search);
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task The_Jarvis_agent_answers_a_simple_request_in_one_call_with_its_own_tools()
    {
        RegisterClockAndSearch();

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Bonsoir"));
        await TurnStoredAsync();

        var call = client.Calls.Single();
        Assert.Multiple(() =>
        {
            Assert.That(Offered(call), Is.EqualTo(new[] { "get_datetime", "hand_to_agent" }), "Never the tools of the other agents (section 4).");
            Assert.That(call.Options?.Tools?.Single(tool => tool.Name == "hand_to_agent").Description, Does.Contain("- Web: "), "One line per agent.");
            Assert.That(call.Options?.Instructions, Does.Contain("Tu reçois chaque demande."), "The instructions of the agent are in the system message.");
            Assert.That(journal.Turns.Single().Agent, Is.EqualTo("Jarvis"));
            Assert.That(journal.Turns.Single().ToolCalls, Is.Empty);
        });
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task A_request_handed_over_continues_with_the_agent_named_its_instructions_and_its_tools()
    {
        var (_, search) = RegisterClockAndSearch();
        client.Rounds.Enqueue([Call("hand_to_agent", "call_1", ("agent", "web"), ("reason", "Une recherche."))]);
        client.Rounds.Enqueue([Call("web_search", "call_2", ("text", "météo Paris"))]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Cherche la météo à Paris."));
        await TurnStoredAsync();

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Has.Count.EqualTo(3), "The Jarvis agent, then the Web agent twice: its call and its answer.");
            Assert.That(Offered(client.Calls[1]), Is.EqualTo(new[] { "web_search", "return_to_jarvis" }));
            Assert.That(client.Calls[1].Options?.Instructions, Does.Contain("Cite la source"));
            Assert.That(client.Calls[1].Options?.Instructions, Does.Not.Contain("Tu reçois chaque demande."));
            Assert.That(client.Calls[1].Messages.Select(message => message.Role), Is.EqualTo(new[] { ChatRole.User }), "The next agent gets the request as the user made it.");
            Assert.That(client.Calls[1].Messages.Single().Text, Is.EqualTo("Cherche la météo à Paris."));

            Assert.That(search.Invoked, Has.Count.EqualTo(1));
            Assert.That(stored.Agent, Is.EqualTo("Web"));
            Assert.That(stored.ToolCalls.Select(call => (call.Name, call.Outcome)), Is.EqualTo(new[] { ("hand_to_agent", ToolCallOutcome.Done), ("web_search", ToolCallOutcome.Done) }));
            Assert.That(stored.ToolCalls[0].Result, Is.EqualTo("La main est passée à l'agent Web, qui répond maintenant."));
            Assert.That(stored.Answer, Is.EqualTo("Bonsoir, Monsieur. Il est neuf heures."));
            Assert.That(stored.Outcome, Is.EqualTo(TurnOutcome.Answered));
            Assert.That(stored.InputTokens, Is.EqualTo(320), "The tokens of the three requests.");
            Assert.That(Published<SentenceToSpeak>(), Is.Empty, "A typed request gets a written answer only.");
        });
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task By_voice_Jarvis_says_that_it_hands_over_when_the_model_did_not()
    {
        RegisterClockAndSearch();
        client.Rounds.Enqueue([Call("hand_to_agent", "call_1", ("agent", "Web"))]);

        var turn = await StartSpokenTurnAsync();
        await Bus.PublishAsync(new UtteranceTranscribed(turn, "Cherche la météo.", AssistantLanguage.French, TimeSpan.FromSeconds(1), TimeSpan.Zero));
        await engine.LastAnswer.WaitAsync(Wait);

        Assert.That(Published<SentenceToSpeak>().Select(sentence => sentence.Text),
            Is.EqualTo(new[] { "Je passe la main à l'agent Web.", "Bonsoir, Monsieur.", "Il est neuf heures." }));
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task By_voice_the_words_of_the_model_are_enough_when_it_said_it_hands_over()
    {
        RegisterClockAndSearch();
        client.Rounds.Enqueue(["Je vérifie cela. ", Call("hand_to_agent", "call_1", ("agent", "Web"))]);

        var turn = await StartSpokenTurnAsync();
        await Bus.PublishAsync(new UtteranceTranscribed(turn, "Cherche la météo.", AssistantLanguage.French, TimeSpan.FromSeconds(1), TimeSpan.Zero));
        await engine.LastAnswer.WaitAsync(Wait);

        Assert.That(Published<SentenceToSpeak>().Select(sentence => sentence.Text),
            Is.EqualTo(new[] { "Je vérifie cela.", "Bonsoir, Monsieur.", "Il est neuf heures." }));
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task An_unknown_agent_is_refused_and_the_Jarvis_agent_keeps_the_hand()
    {
        RegisterClockAndSearch();
        client.Rounds.Enqueue([Call("hand_to_agent", "call_1", ("agent", "Cuisine"))]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Une recette ?"));
        await TurnStoredAsync();

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Has.Count.EqualTo(2));
            Assert.That(Offered(client.Calls[1]), Is.EqualTo(new[] { "get_datetime", "hand_to_agent" }), "Still the Jarvis agent.");
            Assert.That(ResultOf(client.Calls[1].Messages[^1]), Is.EqualTo("La main n'a pas changé : aucun agent ne s'appelle « Cuisine ». Agents connus : Web. Réponds toi-même ou nomme l'un d'eux."));
            Assert.That(stored.ToolCalls.Single().Outcome, Is.EqualTo(ToolCallOutcome.Failed));
            Assert.That(stored.Agent, Is.EqualTo("Jarvis"));
        });
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task An_agent_cannot_call_the_tool_of_another_agent()
    {
        var (_, search) = RegisterClockAndSearch();
        client.Rounds.Enqueue([Call("web_search", "call_1", ("text", "météo"))]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Cherche la météo."));
        await TurnStoredAsync();

        var call = journal.Turns.Single().ToolCalls.Single();
        Assert.Multiple(() =>
        {
            Assert.That(search.Invoked, Is.Empty);
            Assert.That(call.Outcome, Is.EqualTo(ToolCallOutcome.Denied));
            Assert.That(call.Denial, Is.EqualTo("NotThisAgent"));
            Assert.That(call.Result, Is.EqualTo("Refusé : cet outil n'est pas un de ceux de cet agent."));
        });
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task A_typed_request_goes_straight_to_the_agent_the_user_chose()
    {
        RegisterClockAndSearch();
        agentChoice!.Chosen = "web";

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Météo à Paris ?"));
        await TurnStoredAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Offered(client.Calls.Single()), Is.EqualTo(new[] { "web_search", "return_to_jarvis" }), "One call, without the Jarvis agent.");
            Assert.That(journal.Turns.Single().Agent, Is.EqualTo("Web"));
        });
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task By_voice_the_Jarvis_agent_receives_the_request_whatever_the_choice()
    {
        RegisterClockAndSearch();
        agentChoice!.Chosen = "web";

        var turn = await StartSpokenTurnAsync();
        await Bus.PublishAsync(new UtteranceTranscribed(turn, "Bonsoir", AssistantLanguage.French, TimeSpan.FromSeconds(1), TimeSpan.Zero));
        await engine.LastAnswer.WaitAsync(Wait);

        Assert.That(Offered(client.Calls.Single()), Is.EqualTo(new[] { "get_datetime", "hand_to_agent" }));
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task An_agent_gives_back_what_is_not_for_it_and_the_Jarvis_agent_answers_without_handing_over_again()
    {
        RegisterClockAndSearch();
        client.Rounds.Enqueue([Call("hand_to_agent", "call_1", ("agent", "Web"))]);
        client.Rounds.Enqueue([Call("return_to_jarvis", "call_2", ("reason", "Ce n'est pas une recherche."))]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Quelle heure est-il ?"));
        await TurnStoredAsync();

        var stored = journal.Turns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Has.Count.EqualTo(3));
            Assert.That(Offered(client.Calls[2]), Is.EqualTo(new[] { "get_datetime" }), "The hand does not go round.");
            Assert.That(client.Calls[2].Options?.Instructions, Does.Contain("Tu reçois chaque demande."));
            Assert.That(stored.ToolCalls.Select(call => call.Name), Is.EqualTo(new[] { "hand_to_agent", "return_to_jarvis" }));
            Assert.That(stored.Agent, Is.EqualTo("Jarvis"));
            Assert.That(stored.Answer, Is.EqualTo("Bonsoir, Monsieur. Il est neuf heures."));
        });
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task An_agent_file_changed_by_the_user_applies_to_the_next_turn()
    {
        RegisterClockAndSearch();
        agentFiles!.Save("jarvis", "---\nname: Jarvis\ndescription: Tout.\ntools: get_datetime, web_search\n---\nRéponds en alexandrins.");
        agentFiles.Delete("web");

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Bonsoir"));
        await TurnStoredAsync();

        var call = client.Calls.Single();
        Assert.Multiple(() =>
        {
            Assert.That(Offered(call), Is.EqualTo(new[] { "get_datetime", "web_search" }), "Nobody else has a tool: no hand, one call.");
            Assert.That(call.Options?.Instructions, Does.Contain("Réponds en alexandrins."));
        });
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task An_agent_gets_its_own_memory_after_the_memory_of_the_user_and_no_other()
    {
        RegisterClockAndSearch();
        memory.Recall = new Kazo.Jarvis.Kernel.Conversation.MemoryRecall("[Mémoire]\n- L'utilisateur s'appelle Benoit.\n[Fin de la mémoire]", null);
        memory.OfAgents["web"] = "[Mémoire de l'agent]\n- L'utilisateur préfère Météo-Média.\n[Fin de la mémoire de l'agent]";
        memory.OfAgents["files"] = "[Mémoire de l'agent]\n- Le dossier des factures est D:\\Factures.\n[Fin de la mémoire de l'agent]";
        client.Rounds.Enqueue([Call("hand_to_agent", "call_1", ("agent", "web"))]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Cherche la météo à Paris."));
        await TurnStoredAsync();

        Assert.Multiple(() =>
        {
            Assert.That(client.Calls[0].Options?.Instructions, Does.Contain("Benoit").And.Not.Contain("Météo-Média"), "The Jarvis agent has the memory of the user only.");
            Assert.That(client.Calls[1].Options?.Instructions, Does.Contain("Benoit").And.Contain("Météo-Média"), "The agent that answers gets its own memory too (section 5).");
            Assert.That(client.Calls[1].Options?.Instructions, Does.Not.Contain("Factures"), "Never the memory of another agent.");
            Assert.That(client.Calls[1].Options?.Instructions?.IndexOf("Benoit", StringComparison.Ordinal),
                Is.LessThan(client.Calls[1].Options?.Instructions?.IndexOf("Météo-Média", StringComparison.Ordinal)));
        });
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task An_agent_memory_that_fails_never_holds_the_answer()
    {
        RegisterClockAndSearch();
        memory.AgentFailure = new InvalidOperationException("no store");
        client.Rounds.Enqueue([Call("hand_to_agent", "call_1", ("agent", "web"))]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Cherche la météo à Paris."));
        await TurnStoredAsync();

        Assert.That(journal.Turns.Single().Agent, Is.EqualTo("Web"));
    }

    [Test]
    [Property(WithAgents, "yes")]
    public async Task A_tool_made_for_every_agent_is_offered_to_the_others_and_knows_who_calls()
    {
        RegisterClockAndSearch();
        var note = new FakeTool("remember_agent_fact") { ForEveryAgent = true };
        registry.Register(note);
        client.Rounds.Enqueue([Call("hand_to_agent", "call_1", ("agent", "web"))]);
        client.Rounds.Enqueue([Call("remember_agent_fact", "call_2", ("text", "L'utilisateur préfère Météo-Média."))]);

        await Bus.PublishAsync<AssistantInput>(new TextSubmitted("Pour la météo, prends toujours Météo-Média."));
        await TurnStoredAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Offered(client.Calls[0]), Has.None.EqualTo("remember_agent_fact"), "The memory of the Jarvis agent is the memory of the user.");
            Assert.That(Offered(client.Calls[1]), Is.EqualTo(new[] { "web_search", "remember_agent_fact", "return_to_jarvis" }));
            Assert.That(note.Agents, Is.EqualTo(new[] { "web" }), "The fact goes to the memory of the agent that calls.");
        });
    }
}
