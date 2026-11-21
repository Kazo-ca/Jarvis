using System.Text.Json;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Llm.HandOff;
using Kazo.Jarvis.Features.Llm.Providers;
using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Tests.Unit.Adapters;

namespace Kazo.Jarvis.Tests.Unit.Features.Llm;

/// <summary>
/// The hand-off slice (US2.5 to US2.7, ADR 0020, sections 1 to 3): no target by default and then no tool, a target
/// chosen among the cloud providers only, a client that follows the settings, the formulas that force the
/// hand-off, and the tool as the local model sees it.
/// </summary>
[TestFixture]
public sealed class HandOffTests
{
    private TemporaryFolder folder = null!;
    private JsonFileSettingsStore settings = null!;
    private LlmProviderList providers = null!;
    private FakeConnector connector = null!;
    private ToolRegistry registry = null!;
    private HandOffTargets targets = null!;

    [SetUp]
    public async Task SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        await settings.WriteAsync(LlmProvidersOptions.Section, new LlmProvidersOptions
        {
            Providers =
            [
                LlmProvidersOptions.Template(LlmProviderKind.Ollama, LlmProvidersOptions.LocalId, "Ollama"),
                LlmProvidersOptions.Template(LlmProviderKind.Anthropic, "anthropic", "Anthropic"),
                LlmProvidersOptions.Template(LlmProviderKind.OpenAi, "openai", "OpenAI") with { Model = "gpt-5" },
            ],
        });
        providers = new LlmProviderList(settings);
        connector = new FakeConnector();
        registry = new ToolRegistry([], settings);
        targets = new HandOffTargets(settings, providers, connector, registry);
    }

    [TearDown]
    public void TearDown()
    {
        targets.Dispose();
        registry.Dispose();
        providers.Dispose();
        folder.Dispose();
    }

    [Test]
    public void By_default_there_is_no_target_and_the_model_is_not_offered_the_tool()
    {
        Assert.Multiple(() =>
        {
            Assert.That(settings.Read(HandOffOptions.Section).QuestionsProviderId, Is.Null);
            Assert.That(settings.Read(HandOffOptions.Section).WithMemory, Is.False);
            Assert.That(targets.QuestionsTarget, Is.Null);
            Assert.That(registry.Find(HandOffTool.ToolName), Is.Null, "The local model is not offered what it cannot do.");
            Assert.That(targets.Open(HandOffKind.Question), Is.EqualTo(HandOffOpening.Refused(HandOffRefusal.NoQuestionsTarget)));
            Assert.That(connector.Clients, Is.Empty, "Nothing is prepared for a cloud provider nobody chose.");
        });
    }

    [Test]
    public async Task A_chosen_cloud_provider_answers_the_questions_and_the_tool_joins_the_registry()
    {
        await settings.WriteAsync(HandOffOptions.Section, new HandOffOptions { QuestionsProviderId = "anthropic" });

        var opening = targets.Open(HandOffKind.Question);
        var tool = registry.Find(HandOffTool.ToolName)!;
        Assert.Multiple(() =>
        {
            Assert.That(opening.Refusal, Is.Null);
            Assert.That(opening.Target!.Provider.Id, Is.EqualTo("anthropic"));
            Assert.That(opening.Target.Client, Is.SameAs(connector.Clients.Single()));
            Assert.That(opening.Target.Name, Is.EqualTo("Claude"));
            Assert.That(opening.Target.WithMemory, Is.False);
            Assert.That(registry.Enabled, Does.Contain(tool), "Choosing the target is the consent: the tool is on.");
            Assert.That(tool.Origin, Is.EqualTo(ToolOrigins.HandOff));
            Assert.That(tool.DeclaredClass, Is.EqualTo(ActionClass.Read));
            Assert.That(tool.Describe(AssistantLanguage.French), Does.StartWith("Passe la question à Claude"));
            Assert.That(tool.Discloses(AssistantLanguage.French), Does.Contain("partent vers Claude"));
        });
    }

    [Test]
    public async Task The_client_follows_the_target_and_is_withdrawn_with_it()
    {
        await settings.WriteAsync(HandOffOptions.Section, new HandOffOptions { QuestionsProviderId = "anthropic" });
        await settings.WriteAsync(HandOffOptions.Section, new HandOffOptions { QuestionsProviderId = "openai", WithMemory = true });

        var chatGpt = targets.Open(HandOffKind.Question).Target!;
        Assert.Multiple(() =>
        {
            Assert.That(connector.Clients[0].Disposed, Is.True, "The client of the former target is closed.");
            Assert.That(chatGpt.Name, Is.EqualTo("ChatGPT"));
            Assert.That(chatGpt.Provider.Model, Is.EqualTo("gpt-5"));
            Assert.That(chatGpt.WithMemory, Is.True);
            Assert.That(registry.Find(HandOffTool.ToolName)!.Describe(AssistantLanguage.English), Does.Contain("ChatGPT"), "The name of the tool is stable, its description names the target.");
        });

        await settings.WriteAsync(HandOffOptions.Section, new HandOffOptions());

        Assert.Multiple(() =>
        {
            Assert.That(connector.Clients[1].Disposed, Is.True);
            Assert.That(registry.Find(HandOffTool.ToolName), Is.Null);
            Assert.That(targets.Open(HandOffKind.Question).Refusal, Is.EqualTo(HandOffRefusal.NoQuestionsTarget));
        });
    }

    [Test]
    public async Task A_local_provider_or_one_that_is_gone_is_not_a_target()
    {
        await settings.WriteAsync(HandOffOptions.Section, new HandOffOptions { QuestionsProviderId = LlmProvidersOptions.LocalId });
        Assert.That(targets.QuestionsTarget, Is.Null, "Only a cloud provider answers the questions handed over.");

        await settings.WriteAsync(HandOffOptions.Section, new HandOffOptions { QuestionsProviderId = "anthropic" });
        Assert.That(targets.QuestionsTarget?.Id, Is.EqualTo("anthropic"));

        // The provider is removed on the other page.
        var options = settings.Read(LlmProvidersOptions.Section);
        await settings.WriteAsync(LlmProvidersOptions.Section, options with { Providers = [.. options.Providers.Where(provider => provider.Id != "anthropic")] });

        Assert.Multiple(() =>
        {
            Assert.That(targets.QuestionsTarget, Is.Null);
            Assert.That(registry.Find(HandOffTool.ToolName), Is.Null);
            Assert.That(connector.Clients.Single().Disposed, Is.True);
        });
    }

    [Test]
    public async Task A_change_of_the_target_provider_gives_a_new_client()
    {
        await settings.WriteAsync(HandOffOptions.Section, new HandOffOptions { QuestionsProviderId = "anthropic" });
        var options = settings.Read(LlmProvidersOptions.Section);
        await settings.WriteAsync(LlmProvidersOptions.Section, options with
        {
            Providers = [.. options.Providers.Select(provider => provider.Id == "anthropic" ? provider with { Model = "claude-sonnet-5-5" } : provider)],
        });

        Assert.Multiple(() =>
        {
            Assert.That(connector.Clients, Has.Count.EqualTo(2));
            Assert.That(connector.Clients[0].Disposed, Is.True);
            Assert.That(targets.Open(HandOffKind.Question).Target!.Provider.Model, Is.EqualTo("claude-sonnet-5-5"));
        });
    }

    [Test]
    public async Task The_target_of_the_tasks_is_not_there_yet()
    {
        await settings.WriteAsync(HandOffOptions.Section, new HandOffOptions { QuestionsProviderId = "anthropic" });

        Assert.That(targets.Open(HandOffKind.Task), Is.EqualTo(HandOffOpening.Refused(HandOffRefusal.NoTasksTarget)), "A task never goes to the target of the questions.");
    }

    [TestCase("Demande à Claude combien font six fois sept", HandOffKind.Question, "combien font six fois sept")]
    [TestCase("demande à claude, quelle est la capitale du Pérou ?", HandOffKind.Question, "quelle est la capitale du Pérou ?")]
    [TestCase("Demande a Claud pourquoi le ciel est bleu.", HandOffKind.Question, "pourquoi le ciel est bleu.")]
    [TestCase(" Demandez à Clode : qui a écrit Candide", HandOffKind.Question, "qui a écrit Candide")]
    [TestCase("Jarvis, demande à Claude de résumer Kant", HandOffKind.Question, "de résumer Kant")]
    [TestCase("Ask Claude what a monad is", HandOffKind.Question, "what a monad is")]
    [TestCase("ask claud, why is the sky blue?", HandOffKind.Question, "why is the sky blue?")]
    [TestCase("Demande à Claude Code de ranger le dépôt", HandOffKind.Task, "de ranger le dépôt")]
    [TestCase("Ask Claude Code to fix the build", HandOffKind.Task, "to fix the build")]
    [TestCase("Demande à Claude-Code de lancer les tests", HandOffKind.Task, "de lancer les tests")]
    public void A_formula_at_the_start_of_the_request_forces_the_hand_off(string request, HandOffKind kind, string rest)
    {
        Assert.That(targets.Forced(request), Is.EqualTo(new ForcedHandOff(kind, rest)));
    }

    [TestCase("Peux-tu demander à Claude combien font six fois sept")]
    [TestCase("Je me demande à quoi sert Claude")]
    [TestCase("Demande à Claudette si elle vient")]
    [TestCase("Demande à Claude")]
    [TestCase("Ask Claude.")]
    [TestCase("Demande à Marie de venir")]
    [TestCase("")]
    public void Anything_else_goes_to_the_local_model(string request)
    {
        Assert.That(targets.Forced(request), Is.Null);
    }

    [Test]
    public void The_tool_reads_its_kind_and_its_reason()
    {
        static JsonElement Arguments(string json) => JsonDocument.Parse(json).RootElement.Clone();
        var tool = new HandOffTool(() => "Claude");

        Assert.Multiple(() =>
        {
            Assert.That(HandOffTool.Read(Arguments("""{"kind":"question","reason":" Un long raisonnement. "}""")), Is.EqualTo(new HandOffCall(HandOffKind.Question, "Un long raisonnement.")));
            Assert.That(HandOffTool.Read(Arguments("""{"kind":"TASK"}""")), Is.EqualTo(new HandOffCall(HandOffKind.Task, null)));
            Assert.That(HandOffTool.Read(Arguments("{}")), Is.EqualTo(new HandOffCall(HandOffKind.Question, null)), "The only use offered.");
            Assert.That(HandOffTool.Read(Arguments("""{"kind":"email"}""")), Is.Null);
            Assert.That(HandOffTool.Read(Arguments("""{"kind":"question","reason":"%s"}""".Replace("%s", new string('x', 300), StringComparison.Ordinal)))!.Reason, Has.Length.EqualTo(200));
            Assert.That(tool.Classify(Arguments("""{"kind":"question","reason":"r"}""")), Is.EqualTo(ActionClass.Read), "No confirmation at each hand-off.");
            Assert.That(() => tool.Classify(Arguments("""{"kind":"email"}""")), Throws.ArgumentException, "An unknown kind is a denial.");
            Assert.That(tool.Schema.GetProperty("properties").GetProperty("kind").GetProperty("enum").EnumerateArray().Select(kind => kind.GetString()), Is.EqualTo(new[] { "question" }));
            Assert.That(async () => await tool.InvokeAsync(Arguments("{}"), CancellationToken.None), Throws.TypeOf<ToolException>(), "It only runs inside a turn.");
        });
    }

    [Test]
    public void A_target_id_with_a_space_is_refused()
    {
        Assert.That(HandOffOptions.Section.Validate(new HandOffOptions { QuestionsProviderId = "not an id" }), Is.Not.Empty);
    }
}
