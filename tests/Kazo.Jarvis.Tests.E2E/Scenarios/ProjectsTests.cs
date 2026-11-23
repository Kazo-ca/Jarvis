using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The projects on the Web host (work item stage2/19-projects, ADR 0025 sections 6, 10 and 11): the « Projets » page
/// creates a project from a name and a folder, shows its memory file and allows its folder only when asked; the
/// Jarvis agent hands a request about a project to the one « Projet » agent, naming the project; and the user may
/// choose the project of a typed request. The files are simulated and so is the model.
/// </summary>
[TestFixture]
public sealed class ProjectsTests : PageTest
{
    private const string KazoOs = "/home/demo/Projets/kazo-os";

    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Newest => Conversation.Locator("[data-conversation-turn]").First;

    private ILocator Turns => Conversation.Locator("[data-conversation-turn]");

    private ILocator Projects => Settings.Locator("[data-project]");

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
        // The host keeps its settings between the tests: no choice, no project, no allowed folder.
        await CloseDialogsAsync();
        await Page.Locator("#project-memory-reset").DispatchEventAsync("click");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await Page.ClickAsync("#open-conversation");
        await Conversation.Locator("[data-conversation-agent-choice]").SelectOptionAsync(string.Empty);
        var hangUp = Conversation.Locator("[data-conversation-call-end]");
        if (await hangUp.CountAsync() == 1)
        {
            await hangUp.ClickAsync();
        }

        var project = Conversation.Locator("[data-conversation-project-choice]");
        if (await project.CountAsync() == 1)
        {
            await project.SelectOptionAsync(string.Empty);
        }

        await OpenSettingsAsync("Projects.Catalog");
        await Expect(Settings.Locator("[data-project-add-name]")).ToBeVisibleAsync();
        await RemoveAllAsync(Settings.Locator("[data-project-remove]"));
        await OpenSettingsAsync("Tools.Files");
        await Expect(Settings.Locator("[data-allowed-folder-input]")).ToBeVisibleAsync();
        await RemoveAllAsync(Settings.Locator("[data-allowed-folder-remove]"));
        await CloseDialogsAsync();
    }

    [Test]
    public async Task A_project_is_created_from_a_name_and_a_folder_and_its_folder_is_allowed_only_when_asked()
    {
        await OpenSettingsAsync("Projects.Catalog");
        await Expect(Settings.Locator("[data-projects-none]")).ToBeVisibleAsync();
        await Expect(Settings.Locator("[data-project-add]")).ToBeDisabledAsync();

        await AddAsync("Kazo OS", "Projets/kazo-os");
        await Expect(Settings.Locator("[data-projects-problem]")).ToBeVisibleAsync();
        await Expect(Projects).ToHaveCountAsync(0);

        await AddAsync("Kazo OS", KazoOs);
        await Expect(Projects).ToHaveCountAsync(1);
        await Expect(Settings.Locator("[data-projects-problem]")).ToHaveCountAsync(0);
        await Expect(Settings.Locator("[data-project-add-name]")).ToHaveValueAsync(string.Empty);
        var kazo = Settings.Locator("[data-project='kazo-os']");
        await Expect(kazo.Locator("[data-project-name]")).ToHaveTextAsync("Kazo OS");
        await Expect(kazo.Locator("[data-project-folder]")).ToHaveTextAsync(KazoOs);
        await Expect(kazo).ToHaveAttributeAsync("data-project-memory", "true");
        await Expect(kazo.Locator("[data-project-memory-text]")).ToContainTextAsync("le pilote réseau");

        // Two projects cannot share a name, and a folder without the file has no memory yet.
        await AddAsync("kazo os", "/home/demo/Documents");
        await Expect(Settings.Locator("[data-projects-problem]")).ToBeVisibleAsync();
        await Expect(Projects).ToHaveCountAsync(1);
        await AddAsync("Recettes", "/home/demo/Documents/recettes");
        await Expect(Settings.Locator("[data-project='recettes']")).ToHaveAttributeAsync("data-project-memory", "false");

        // Creating a project allowed nothing (section 6): the page proposes, and the user confirms.
        await Expect(kazo).ToHaveAttributeAsync("data-project-allowed", "false");
        await kazo.Locator("[data-project-allow]").ClickAsync();
        await kazo.Locator("[data-project-allow-cancel]").ClickAsync();
        await Expect(kazo).ToHaveAttributeAsync("data-project-allowed", "false");
        await kazo.Locator("[data-project-allow]").ClickAsync();
        await kazo.Locator("[data-project-allow-confirm]").ClickAsync();
        await Expect(kazo).ToHaveAttributeAsync("data-project-allowed", "true");
        await OpenSettingsAsync("Tools.Files");
        await Expect(Settings.Locator($"[data-allowed-folder='{KazoOs}']")).ToHaveCountAsync(1);

        await OpenSettingsAsync("Projects.Catalog");
        await Settings.Locator("[data-project='recettes'] [data-project-remove]").ClickAsync();
        await Expect(Projects).ToHaveCountAsync(1);
    }

    [Test]
    public async Task The_Jarvis_agent_hands_a_request_about_a_project_to_the_project_agent_with_its_memory()
    {
        await OpenSettingsAsync("Projects.Catalog");
        await AddAsync("Kazo OS", KazoOs);
        await Expect(Projects).ToHaveCountAsync(1);

        await OpenConversationAsync();
        var before = await Turns.CountAsync();
        await SendAsync("Il reste quoi à faire dans Kazo OS ?");
        await Expect(Turns).ToHaveCountAsync(before + 1);
        await Expect(Newest.Locator("[data-conversation-tool='hand_to_agent']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 15_000 });
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-agent", new Regex("Projet|Project"));
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-project", "Kazo OS");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync(KazoOs);
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("le pilote réseau");
        await ExpectIdleAsync();

        // A request that names no project is about none: the Jarvis agent answers, and no project is shown.
        await SendAsync("Bonsoir");
        await Expect(Turns).ToHaveCountAsync(before + 2);
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 15_000 });
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-project", string.Empty);
        await ExpectIdleAsync();
    }

    [Test]
    public async Task The_project_of_a_typed_request_can_be_chosen_in_the_conversation_window()
    {
        await OpenConversationAsync();
        await Expect(Conversation.Locator("[data-conversation-project-choice]")).ToHaveCountAsync(0);

        await OpenSettingsAsync("Projects.Catalog");
        await AddAsync("Kazo OS", KazoOs);
        await AddAsync("Recettes", "/home/demo/Documents/recettes");
        await Expect(Projects).ToHaveCountAsync(2);

        await OpenConversationAsync();
        var project = Conversation.Locator("[data-conversation-project-choice]");
        await Expect(project.Locator("option")).ToHaveCountAsync(3);
        await Conversation.Locator("[data-conversation-agent-choice]").SelectOptionAsync("project");
        await project.SelectOptionAsync("recettes");
        await Expect(project).ToHaveValueAsync("recettes");

        var before = await Turns.CountAsync();
        await SendAsync("Que retiens-tu ?");
        await Expect(Turns).ToHaveCountAsync(before + 1);
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 15_000 });
        await Expect(Newest.Locator("[data-conversation-tool='hand_to_agent']")).ToHaveCountAsync(0);
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-project", "Recettes");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("/home/demo/Documents/recettes");
        await Expect(Newest.Locator("[data-conversation-answer]")).Not.ToContainTextAsync("le pilote réseau");
        await ExpectIdleAsync();
    }

    [Test]
    public async Task A_session_in_a_folder_of_no_project_asks_where_it_belongs_and_the_user_answers()
    {
        // A Claude Code session the user started by hand works in a folder Jarvis has no project for (section 6).
        const string bar = "/home/demo/projets/kazo-bar";
        await Page.Locator("#hand-session-work").DispatchEventAsync("click");
        await OpenSettingsAsync("Projects.Catalog");
        var request = Settings.Locator($"[data-project-request='{bar}']");
        await Expect(request).ToHaveCountAsync(1);
        await Expect(request.Locator("[data-project-request-project]")).ToHaveCountAsync(0);
        await Expect(request.Locator("[data-project-request-create]")).ToBeDisabledAsync();

        // A new project on that folder: the request is answered.
        await request.Locator("[data-project-request-name]").FillAsync("Kazo Bar");
        await request.Locator("[data-project-request-create]").ClickAsync();
        await Expect(request).ToHaveCountAsync(0);
        await Expect(Settings.Locator("[data-project='kazo-bar'] [data-project-folder]")).ToHaveTextAsync(bar);

        // Without that project the request is back, and it can go to a project that exists.
        await Settings.Locator("[data-project='kazo-bar'] [data-project-remove]").ClickAsync();
        await Expect(request).ToHaveCountAsync(1);
        await AddAsync("Kazo OS", KazoOs);
        await Expect(Projects).ToHaveCountAsync(1);
        await Expect(request.Locator("[data-project-request-assign]")).ToBeDisabledAsync();
        await request.Locator("[data-project-request-project]").SelectOptionAsync("kazo-os");
        await request.Locator("[data-project-request-assign]").ClickAsync();
        await Expect(request).ToHaveCountAsync(0);
        var more = Settings.Locator($"[data-project='kazo-os'] [data-project-more-folder='{bar}']");
        await Expect(more).ToHaveCountAsync(1);

        // Taken back from the project, the folder asks again: Jarvis never chose.
        await more.Locator("[data-project-more-remove]").ClickAsync();
        await Expect(request).ToHaveCountAsync(1);
        await Page.Locator("#hand-session-stop").DispatchEventAsync("click");
    }

    [Test]
    public async Task A_call_keeps_the_agent_and_the_project_from_one_turn_to_the_next_until_goodbye()
    {
        await OpenSettingsAsync("Projects.Catalog");
        await AddAsync("Kazo OS", KazoOs);
        await Expect(Projects).ToHaveCountAsync(1);

        // « On doit discuter de… » starts the call (ADR 0025, section 12): the Jarvis agent sorts as usual.
        await OpenConversationAsync();
        var call = Conversation.Locator("[data-conversation-call]");
        await Expect(call).ToHaveAttributeAsync("data-conversation-call", "false");
        var before = await Turns.CountAsync();
        await SendAsync("On doit discuter de ce qu'il reste à faire dans Kazo OS.");
        await Expect(Turns).ToHaveCountAsync(before + 1);
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 15_000 });
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-agent", new Regex("Projet|Project"));
        await Expect(call).ToHaveAttributeAsync("data-conversation-call", "true");
        await Expect(call).ToHaveAttributeAsync("data-conversation-call-agent", "project");
        await ExpectIdleAsync();

        // The next request goes straight to the agent that kept the hand, with the project of the call.
        await SendAsync("Bonsoir");
        await Expect(Turns).ToHaveCountAsync(before + 2);
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 15_000 });
        await Expect(Newest.Locator("[data-conversation-tool='hand_to_agent']")).ToHaveCountAsync(0);
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-agent", new Regex("Projet|Project"));
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-project", "Kazo OS");
        await ExpectIdleAsync();

        // Goodbye ends the call; the next request is sorted by the Jarvis agent again.
        await SendAsync("Au revoir");
        await Expect(Turns).ToHaveCountAsync(before + 3);
        await Expect(Newest.Locator("[data-conversation-answer]")).ToHaveTextAsync(new Regex("Au revoir|Goodbye"));
        await Expect(call).ToHaveAttributeAsync("data-conversation-call", "false");
        await ExpectIdleAsync();
        await SendAsync("Bonsoir");
        await Expect(Turns).ToHaveCountAsync(before + 4);
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 15_000 });
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-agent", "Jarvis");
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-project", string.Empty);
        await ExpectIdleAsync();

        // The button starts a call too, and Jarvis listens right away; a listening the user cancels is not started again.
        await call.Locator("[data-conversation-call-start]").ClickAsync();
        await Expect(call).ToHaveAttributeAsync("data-conversation-call", "true");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Page.Locator("#simulate-hotkey").DispatchEventAsync("click");
        await ExpectIdleAsync();
        await call.Locator("[data-conversation-call-end]").ClickAsync();
        await Expect(call).ToHaveAttributeAsync("data-conversation-call", "false");
        await ExpectIdleAsync();
    }

    [Test]
    public async Task A_fact_is_written_in_the_memory_of_a_project_only_after_the_user_confirms()
    {
        await OpenSettingsAsync("Projects.Catalog");
        await AddAsync("Recettes", "/home/demo/Documents/recettes");
        var recettes = Settings.Locator("[data-project='recettes']");
        await Expect(recettes).ToHaveAttributeAsync("data-project-memory", "false");

        // Refused: nothing is written, and the model is told so.
        await OpenConversationAsync();
        var before = await Turns.CountAsync();
        var confirmation = Page.Locator("[data-dialog-key='Permissions.Confirmation']");
        await SendAsync("Retiens pour le projet Recettes que la tarte se cuit à 180 degrés.");
        await Expect(confirmation.Locator("[data-confirmation-summary]")).ToHaveTextAsync(new Regex("Recettes.*la tarte se cuit à 180 degrés"), new() { Timeout = 15_000 });
        await confirmation.Locator("[data-confirmation-refuse]").ClickAsync();
        await Expect(Turns).ToHaveCountAsync(before + 1);
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 15_000 });
        await Expect(Newest.Locator("[data-conversation-tool='remember_project_fact']")).Not.ToHaveAttributeAsync("data-tool-outcome", "Done");
        await ExpectIdleAsync();
        await OpenSettingsAsync("Projects.Catalog");
        await Expect(recettes).ToHaveAttributeAsync("data-project-memory", "false");

        // Approved: the file of the folder is created, with the fact.
        await OpenConversationAsync();
        await SendAsync("Retiens pour le projet Recettes que la tarte se cuit à 180 degrés.");
        await Expect(confirmation.Locator("[data-confirmation-approve]")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await confirmation.Locator("[data-confirmation-approve]").ClickAsync();
        await Expect(Turns).ToHaveCountAsync(before + 2);
        await Expect(Newest.Locator("[data-conversation-tool='remember_project_fact']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 15_000 });
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 15_000 });
        await ExpectIdleAsync();
        await OpenSettingsAsync("Projects.Catalog");
        await Expect(recettes).ToHaveAttributeAsync("data-project-memory", "true");
        await Expect(recettes.Locator("[data-project-memory-text]")).ToContainTextAsync("la tarte se cuit à 180 degrés");
        await Page.Locator("#project-memory-reset").DispatchEventAsync("click");
    }

    private async Task AddAsync(string name, string folder)
    {
        await Settings.Locator("[data-project-add-name]").FillAsync(name);
        await Settings.Locator("[data-project-add-folder]").FillAsync(folder);
        await Expect(Settings.Locator("[data-project-add]")).ToBeEnabledAsync();
        await Settings.Locator("[data-project-add]").ClickAsync();
    }

    private async Task RemoveAllAsync(ILocator remove)
    {
        for (var left = await remove.CountAsync(); left > 0; left--)
        {
            await remove.First.ClickAsync();
            await Expect(remove).ToHaveCountAsync(left - 1);
        }
    }

    private async Task OpenSettingsAsync(string page)
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-settings");
        await Settings.Locator($"[data-settings-page='{page}']").ClickAsync();
    }

    private async Task OpenConversationAsync()
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-conversation");
        await Expect(Conversation.Locator("[data-conversation-window]")).ToBeVisibleAsync();
    }

    /// <summary>Sends from the open conversation window, once the window can take the request.</summary>
    private async Task SendAsync(string request)
    {
        await Conversation.Locator("[data-conversation-text]").FillAsync(request);

        // The button is on once the window knows the text and Jarvis is idle: Enter before that is lost.
        await Expect(Conversation.Locator("[data-conversation-send]")).ToBeEnabledAsync();
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
