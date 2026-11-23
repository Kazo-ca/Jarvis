using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The agents on the Web host (work item stage2/17-agents, ADR 0025 sections 1 to 4, 8 and 9): the « Agents » page
/// lists the starting agents read from their files, a file that does not read is named with its fault, the Jarvis
/// agent answers a simple request itself and hands the others to the agent that has the tool, and the user may choose
/// the agent of a typed request. The files are in memory and the model is simulated.
/// </summary>
[TestFixture]
public sealed class AgentsTests : PageTest
{
    private const string Documents = "/home/demo/Documents";

    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Newest => Conversation.Locator("[data-conversation-turn]").First;

    private ILocator Choice => Conversation.Locator("[data-conversation-agent-choice]");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
    }

    [TearDown]
    public async Task Restore()
    {
        // The host keeps its files and its settings between the tests: no kitchen agent, no allowed folder, no choice.
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await Page.ClickAsync("#agent-file-delete");
        await Page.ClickAsync("#open-conversation");
        await Choice.SelectOptionAsync(string.Empty);
        await OpenSettingsAsync("Tools.Files");
        var remove = Settings.Locator("[data-allowed-folder-remove]");
        for (var left = await remove.CountAsync(); left > 0; left--)
        {
            await remove.First.ClickAsync();
            await Expect(remove).ToHaveCountAsync(left - 1);
        }

        await CloseDialogsAsync();
    }

    [Test]
    public async Task The_agents_page_lists_the_starting_agents_with_the_tools_each_one_shows()
    {
        await OpenSettingsAsync("Agents.Catalog");

        await Expect(Settings.Locator("[data-agents-folder]")).ToContainTextAsync("agents");
        await Expect(Settings.Locator("[data-agent]")).ToHaveCountAsync(6);
        await Expect(Settings.Locator("[data-agent]").First).ToHaveAttributeAsync("data-agent", "jarvis");
        await Expect(Settings.Locator("[data-agents-issues]")).ToHaveCountAsync(0);

        // The Jarvis agent shows its own tools, never the hand tools nor those of the others.
        var jarvis = Settings.Locator("[data-agent='jarvis']");
        await Expect(jarvis.Locator("[data-agent-tool='get_datetime']")).ToHaveCountAsync(1);
        await Expect(jarvis.Locator("[data-agent-tool='calculate']")).ToHaveCountAsync(1);
        await Expect(jarvis.Locator("[data-agent-tool='hand_to_agent']")).ToHaveCountAsync(0);
        await Expect(jarvis.Locator("[data-agent-tool='read_file']")).ToHaveCountAsync(0);

        var files = Settings.Locator("[data-agent='files']");
        await Expect(files.Locator("[data-agent-name]")).ToHaveTextAsync(new Regex("Fichiers|Files"));
        await Expect(files).ToHaveAttributeAsync("data-agent-available", "true");
        // Its two tools, and the one every agent has to keep its own memory (section 5).
        await Expect(files.Locator("[data-agent-tool]")).ToHaveCountAsync(3);
        await Expect(files.Locator("[data-agent-tool='remember_agent_fact']")).ToHaveCountAsync(1);
        await Expect(jarvis.Locator("[data-agent-tool='remember_agent_fact']")).ToHaveCountAsync(0);

        // The web tools are off until the user turns them on: the agent names them, and nobody hands over to it.
        var web = Settings.Locator("[data-agent='web']");
        await Expect(web).ToHaveAttributeAsync("data-agent-available", "false");
        await Expect(web.Locator("[data-agent-no-tools]")).ToBeVisibleAsync();
        await Expect(web.Locator("[data-agent-tools-off]")).ToContainTextAsync("web_search");

        // The button opens the file in the editor of the user.
        await files.Locator("[data-agent-open]").ClickAsync();
        await Expect(Settings.Locator("[data-agents-problem]")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task A_file_that_does_not_read_is_named_with_its_fault_until_it_is_fixed()
    {
        await Page.ClickAsync("#agent-file-break");
        await Expect(Page.Locator("#fault")).ToHaveAttributeAsync("data-cause", "AgentUnreadable");

        await OpenSettingsAsync("Agents.Catalog");
        var issue = Settings.Locator("[data-agent-issue='cuisine']");
        await Expect(issue).ToHaveAttributeAsync("data-agent-problem", "NoHeader");
        await Expect(issue).ToContainTextAsync("cuisine.md");
        await Expect(Settings.Locator("[data-agent='cuisine']")).ToHaveCountAsync(0);

        // The file is saved again with its header: it is read at once, and the fault goes away.
        await CloseDialogsAsync();
        await Page.ClickAsync("#agent-file-write");
        await Expect(Page.Locator("#fault")).ToHaveAttributeAsync("data-cause", string.Empty);
        await OpenSettingsAsync("Agents.Catalog");
        await Expect(Settings.Locator("[data-agents-issues]")).ToHaveCountAsync(0);
        await Expect(Settings.Locator("[data-agent='cuisine'] [data-agent-name]")).ToHaveTextAsync("Cuisine");
        await Expect(Settings.Locator("[data-agent='cuisine'] [data-agent-description]")).ToContainTextAsync("recettes");
    }

    [Test]
    public async Task The_Jarvis_agent_answers_a_simple_request_itself_and_hands_a_file_request_to_the_files_agent()
    {
        await AllowDocumentsAsync();

        // A simple request: the Jarvis agent has the clock, one call, no hand.
        await TypeAsync("Donne-moi l'heure exacte.");
        await Expect(Newest.Locator("[data-conversation-tool='get_datetime']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-tool='hand_to_agent']")).ToHaveCountAsync(0);
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-agent", "Jarvis");
        await ExpectIdleAsync();

        // A request for a tool the Jarvis agent does not see: it hands over, and the files agent reads.
        await TypeAsync($"Lis le fichier {Documents}/notes.txt");
        await Expect(Newest.Locator("[data-conversation-tool='hand_to_agent']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-tool='read_file']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("farine");
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-agent", new Regex("Fichiers|Files"));
        await Expect(Newest.Locator("[data-conversation-meta]")).ToContainTextAsync(new Regex("Agent (Fichiers|Files)"));
        await ExpectIdleAsync();
    }

    [Test]
    public async Task A_name_no_agent_has_is_refused_and_the_Jarvis_agent_keeps_the_hand()
    {
        await TypeAsync("Passe la main à Jardinage.");

        var hand = Newest.Locator("[data-conversation-tool='hand_to_agent']");
        await Expect(hand).ToHaveAttributeAsync("data-tool-outcome", "Failed", new() { Timeout = 10_000 });
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Jardinage");
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-agent", "Jarvis");
    }

    [Test]
    public async Task The_agent_chosen_in_the_window_answers_a_typed_request_without_the_Jarvis_agent()
    {
        await AllowDocumentsAsync();
        await Page.ClickAsync("#open-conversation");
        await Expect(Choice.Locator("option")).ToHaveCountAsync(6);
        await Choice.SelectOptionAsync("files");

        await Conversation.Locator("[data-conversation-text]").FillAsync($"Lis le fichier {Documents}/notes.txt");
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");
        await Expect(Newest.Locator("[data-conversation-tool='read_file']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-tool='hand_to_agent']")).ToHaveCountAsync(0);
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-agent", new Regex("Fichiers|Files"));
        await ExpectIdleAsync();

        // What is not for the chosen agent goes back to the Jarvis agent, which answers.
        await Conversation.Locator("[data-conversation-text]").FillAsync("Ceci n'est pas pour toi : donne-moi l'heure exacte.");
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");
        await Expect(Newest.Locator("[data-conversation-tool='return_to_jarvis']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-tool='get_datetime']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-agent", "Jarvis");
        await ExpectIdleAsync();
    }

    private async Task AllowDocumentsAsync()
    {
        await OpenSettingsAsync("Tools.Files");
        await Settings.Locator("[data-allowed-folder-input]").FillAsync(Documents);
        await Settings.Locator("[data-allowed-folder-add]").ClickAsync();
        await Expect(Settings.Locator($"[data-allowed-folder='{Documents}']")).ToHaveAttributeAsync("data-folder-usable", "true");
        await CloseDialogsAsync();
    }

    private async Task OpenSettingsAsync(string page)
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-settings");
        await Settings.Locator($"[data-settings-page='{page}']").ClickAsync();
    }

    private async Task TypeAsync(string request)
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-conversation");
        await Expect(Conversation.Locator("[data-conversation-window]")).ToBeVisibleAsync();
        await Conversation.Locator("[data-conversation-text]").FillAsync(request);
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");
    }

    private Task ExpectIdleAsync() => Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 10_000 });

    private async Task CloseDialogsAsync()
    {
        // The dialogs live in the host, not in the page: a window left open would cover the buttons of the next test.
        if (await Settings.CountAsync() == 1)
        {
            // The settings window remembers its page; the next test starts on the first one.
            await Settings.Locator("[data-settings-page='Settings.Language']").ClickAsync();
        }

        var dialogs = Page.Locator(".dialog-layer");
        for (var open = await dialogs.CountAsync(); open > 0; open--)
        {
            await dialogs.Last.Locator(".dialog-close").ClickAsync();
            await Expect(dialogs).ToHaveCountAsync(open - 1);
        }
    }
}
