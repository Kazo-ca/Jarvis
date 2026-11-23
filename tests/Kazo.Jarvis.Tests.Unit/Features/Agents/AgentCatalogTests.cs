using System.Text.Json;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Agents.Catalog;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Agents;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Features.Settings;
using Kazo.Jarvis.Tests.Unit.Kernel.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Agents;

/// <summary>
/// The agents (ADR 0025, sections 1 to 3): the starting files written once and never over a file of the user, the
/// tools each agent shows, the file left out with its fault, and who may take the hand.
/// </summary>
[TestFixture]
public sealed class AgentCatalogTests
{
    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private JsonFileSettingsStore settings = null!;
    private ToolRegistry registry = null!;
    private ManualLanguage language = null!;
    private FakeAgentFiles files = null!;
    private AgentCatalog catalog = null!;

    private IAssistantStateReader State => provider.GetRequiredService<IAssistantStateReader>();

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        provider = new ServiceCollection().AddJarvisKernel().BuildServiceProvider();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        registry = new ToolRegistry([], settings);
        language = new ManualLanguage { Current = AssistantLanguage.French };
        files = new FakeAgentFiles();
        catalog = NewCatalog();
        registry.Register(new HandToAgentTool(() => catalog));
        registry.Register(new ReturnToJarvisTool());
    }

    [TearDown]
    public void TearDown()
    {
        catalog.Dispose();
        registry.Dispose();
        provider.Dispose();
        folder.Dispose();
    }

    [Test]
    public async Task The_first_start_writes_the_starting_agents_in_the_language_of_the_assistant()
    {
        await catalog.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(files.Written, Is.EquivalentTo(new[] { "jarvis", "home-automation", "web", "files", "claude-code", "memory" }));
            Assert.That(catalog.Agents.Select(agent => agent.Name), Is.EqualTo(new[] { "Jarvis", "Claude Code", "Domotique", "Fichiers", "Mémoire", "Web" }), "The Jarvis agent first, then by name.");
            Assert.That(catalog.Main.Id, Is.EqualTo(AgentIds.Main));
            Assert.That(catalog.Find("web")!.Tools, Is.EqualTo(new[] { "web_search", "fetch_url" }));
            Assert.That(catalog.Issues, Is.Empty);
            Assert.That(State.Current.Fault, Is.Null);
        });
    }

    [Test]
    public async Task A_file_the_user_changed_or_deleted_is_never_written_again()
    {
        await catalog.StartAsync(CancellationToken.None);
        files.Save("web", "---\nname: Toile\ndescription: Chercher à ma façon.\ntools: web_search\n---\nMes consignes.");
        files.Delete("memory");
        files.Written.Clear();

        // Another start, in another language: what Jarvis ships changed for every file.
        language.Current = AssistantLanguage.English;
        using var restarted = NewCatalog();
        await restarted.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(files.Written, Is.EquivalentTo(new[] { "jarvis", "home-automation", "files", "claude-code" }), "The untouched files follow; the user's two do not.");
            Assert.That(restarted.Find("web")!.Name, Is.EqualTo("Toile"));
            Assert.That(restarted.Find("web")!.Instructions, Is.EqualTo("Mes consignes."));
            Assert.That(restarted.Find("memory"), Is.Null, "A deleted agent stays deleted.");
            Assert.That(restarted.Find("files")!.Name, Is.EqualTo("Files"));
        });
    }

    [Test]
    public async Task A_second_start_with_nothing_new_writes_nothing()
    {
        await catalog.StartAsync(CancellationToken.None);
        files.Written.Clear();

        using var restarted = NewCatalog();
        await restarted.StartAsync(CancellationToken.None);

        Assert.That(files.Written, Is.Empty);
    }

    [Test]
    public async Task An_agent_shows_its_enabled_tools_only_and_the_Jarvis_agent_takes_what_nobody_claims()
    {
        registry.Register(new FakeTool("get_datetime"));
        registry.Register(new FakeTool("web_search"));
        registry.Register(new FakeTool("fetch_url") { EnabledByDefault = false });
        registry.Register(new FakeTool("brand_new_tool"));
        registry.Register(new AssignedTool("HassTurnOn", AgentIds.HomeAutomation));
        registry.Register(new AssignedTool("lookup_stock", "no-such-agent"));
        await catalog.StartAsync(CancellationToken.None);

        string[] Names(string agent) => [.. catalog.ToolsOf(catalog.Find(agent)!).Select(tool => tool.Name)];
        Assert.Multiple(() =>
        {
            Assert.That(Names("web"), Is.EqualTo(new[] { "web_search", "return_to_jarvis" }), "A tool that is off stays invisible, even named by the file.");
            Assert.That(Names("home-automation"), Is.EqualTo(new[] { "HassTurnOn", "return_to_jarvis" }), "Given by its server, not by the file.");
            Assert.That(Names("jarvis"), Is.EqualTo(new[] { "brand_new_tool", "get_datetime", "lookup_stock", "hand_to_agent" }), "Its own, what no agent claims, then the hand.");
            Assert.That(Names("memory"), Is.EqualTo(new[] { "return_to_jarvis" }));
            Assert.That(catalog.Others.Select(agent => agent.Id), Is.EquivalentTo(new[] { "web", "home-automation" }), "Only an agent with a tool of its own takes the hand.");
        });
    }

    [Test]
    public async Task A_tool_made_for_every_agent_goes_to_all_but_the_Jarvis_agent_and_gives_nobody_the_hand()
    {
        registry.Register(new FakeTool("get_datetime"));
        registry.Register(new FakeTool("web_search"));
        registry.Register(new FakeTool("remember_agent_fact") { ForEveryAgent = true });
        await catalog.StartAsync(CancellationToken.None);

        string[] Names(string agent) => [.. catalog.ToolsOf(catalog.Find(agent)!).Select(tool => tool.Name)];
        Assert.Multiple(() =>
        {
            Assert.That(Names("web"), Is.EqualTo(new[] { "web_search", "remember_agent_fact", "return_to_jarvis" }));
            Assert.That(Names("files"), Is.EqualTo(new[] { "remember_agent_fact", "return_to_jarvis" }));
            Assert.That(Names("jarvis"), Is.EqualTo(new[] { "get_datetime", "hand_to_agent" }), "Its memory is the memory of the user (section 5).");
            Assert.That(catalog.Others.Select(agent => agent.Id), Is.EqualTo(new[] { "web" }), "A tool every agent has is not a reason to take the hand.");
        });
    }

    [Test]
    public async Task Without_another_agent_that_has_a_tool_the_hand_is_not_offered()
    {
        registry.Register(new FakeTool("get_datetime"));
        await catalog.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(catalog.ToolsOf(catalog.Main).Select(tool => tool.Name), Is.EqualTo(new[] { "get_datetime" }), "One call to the model, and nothing to sort.");
            Assert.That(catalog.Others, Is.Empty);
        });
    }

    [Test]
    public async Task The_hand_tool_lists_one_line_per_agent_that_can_take_it()
    {
        registry.Register(new FakeTool("web_search"));
        registry.Register(new FakeTool("read_file"));
        await catalog.StartAsync(CancellationToken.None);
        var hand = registry.Find(HandToAgentTool.ToolName)!;

        var description = hand.Describe(AssistantLanguage.French);
        var names = hand.Schema.GetProperty("properties").GetProperty("agent").GetProperty("enum").EnumerateArray().Select(name => name.GetString());

        Assert.Multiple(() =>
        {
            Assert.That(names, Is.EqualTo(new[] { "Fichiers", "Web" }));
            Assert.That(description, Does.Contain("\n- Web: Chercher sur le web et lire une page."));
            Assert.That(description, Does.Contain("\n- Fichiers: "));
            Assert.That(description, Does.Not.Contain("Domotique"), "An agent without a tool is not listed.");
            Assert.That(description, Does.Not.Contain("web_search"), "Never the tools of the others (section 4).");
        });
    }

    [TestCase("Web", "web")]
    [TestCase("  mémoire ", "memory")]
    [TestCase("MEMOIRE", "memory")]
    [TestCase("claude code", "claude-code")]
    [TestCase("home-automation", "home-automation")]
    [TestCase("Cuisine", null)]
    [TestCase("", null)]
    public async Task An_agent_is_found_by_its_name_or_identity_whatever_the_case_and_accents(string asked, string? expected)
    {
        await catalog.StartAsync(CancellationToken.None);

        Assert.That(catalog.Find(asked)?.Id, Is.EqualTo(expected));
    }

    [Test]
    public async Task A_hand_names_an_agent_that_can_take_it_or_nobody()
    {
        registry.Register(new FakeTool("web_search"));
        await catalog.StartAsync(CancellationToken.None);

        AgentHand Read(string json) => catalog.ReadHand(JsonSerializer.Deserialize<JsonElement>(json));
        Assert.Multiple(() =>
        {
            Assert.That(Read("""{"agent":"web"}""").Agent?.Id, Is.EqualTo("web"));
            Assert.That(Read("""{"agent":"Mémoire"}""").Agent, Is.Null, "It exists, but has no tool now.");
            Assert.That(Read("""{"agent":"Cuisine"}""").Asked, Is.EqualTo("Cuisine"));
            Assert.That(Read("""{"agent":"Cuisine"}""").Agent, Is.Null);
            Assert.That(Read("{}").Agent, Is.Null);
        });
    }

    [Test]
    public async Task A_file_that_does_not_read_is_left_out_and_a_fault_says_so_until_it_reads()
    {
        await catalog.StartAsync(CancellationToken.None);

        files.Save("cuisine", "Pas d'en-tête.");
        var faulty = State.Current.Fault;
        var left = catalog.Issues.ToList();
        var known = catalog.Find("cuisine");

        files.Save("cuisine", "---\nname: Cuisine\ndescription: Les recettes.\n---\n");

        Assert.Multiple(() =>
        {
            Assert.That(faulty, Is.EqualTo(new AssistantFault(FaultCause.AgentUnreadable, "cuisine", FaultOrigin.Agents)));
            Assert.That(left, Is.EqualTo(new[] { new AgentFileIssue("cuisine", AgentFileProblem.NoHeader) }));
            Assert.That(known, Is.Null);
            Assert.That(catalog.Find("Cuisine")?.Description, Is.EqualTo("Les recettes."), "Read again as soon as it changed.");
            Assert.That(catalog.Issues, Is.Empty);
            Assert.That(State.Current.Fault, Is.Null);
        });
    }

    [Test]
    public async Task Two_files_cannot_give_the_same_name()
    {
        await catalog.StartAsync(CancellationToken.None);

        files.Save("toile", "---\nname: WEB\ndescription: Un double.\n---\n");

        Assert.Multiple(() =>
        {
            Assert.That(catalog.Issues.Select(issue => issue.Problem), Does.Contain(AgentFileProblem.NameTaken));
            Assert.That(catalog.Agents.Count(agent => AgentCatalog.Plain(agent.Name) == "web"), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Without_its_file_the_Jarvis_agent_is_the_one_Jarvis_ships_with()
    {
        files.RefuseWrites = true;
        registry.Register(new FakeTool("get_datetime"));

        await catalog.StartAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(catalog.Agents.Select(agent => agent.Id), Is.EqualTo(new[] { AgentIds.Main }));
            Assert.That(catalog.FileIds, Is.Empty);
            Assert.That(catalog.Main.Instructions, Does.Contain("hand_to_agent"));
            Assert.That(catalog.ToolsOf(catalog.Main).Select(tool => tool.Name), Is.EqualTo(new[] { "get_datetime" }));
        });
    }

    [Test]
    public async Task Listeners_are_told_when_the_agents_change_and_the_page_opens_a_file()
    {
        await catalog.StartAsync(CancellationToken.None);
        var told = 0;
        catalog.Changed += () => told++;

        files.Delete("web");

        Assert.Multiple(() =>
        {
            Assert.That(told, Is.EqualTo(1));
            Assert.That(catalog.Find("web"), Is.Null);
            Assert.That(catalog.Open("files"), Is.True);
            Assert.That(catalog.Open("web"), Is.False);
            Assert.That(files.Opened, Is.EqualTo(new[] { "files", "web" }));
        });
    }

    private AgentCatalog NewCatalog() =>
        new(files, registry, settings, language, provider.GetRequiredService<IMessageBus>(), NullLogger<AgentCatalog>.Instance);

    /// <summary>A tool whose source gives it to an agent, like the tools of a Home Assistant server.</summary>
    private sealed class AssignedTool(string name, string agent) : ITool
    {
        private readonly FakeTool inner = new(name);

        public string Name => inner.Name;

        public string Origin => ToolOrigins.Mcp("server");

        public bool EnabledByDefault => true;

        public Kazo.Jarvis.Kernel.Permissions.ActionClass DeclaredClass => inner.DeclaredClass;

        public JsonElement Schema => inner.Schema;

        public string? Agent => agent;

        public string Describe(AssistantLanguage language) => inner.Describe(language);

        public Kazo.Jarvis.Kernel.Permissions.ActionClass Classify(JsonElement arguments) => inner.Classify(arguments);

        public ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken) => inner.InvokeAsync(arguments, cancellationToken);
    }
}
