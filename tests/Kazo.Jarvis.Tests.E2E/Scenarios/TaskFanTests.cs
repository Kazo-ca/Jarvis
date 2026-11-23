using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The task fan on the Web host (work item stage2/14-task-fan; ADR 0010; ADR 0027; ADR 0013, section 6): a right
/// click on the orb opens it and never starts listening, a second one, Escape or a click beside it puts it away; a task has
/// its panel, amber when it waits, with its sub-tasks; the write of a session started by Jarvis is allowed from its
/// panel; the fan opens toward the centre from any corner.
/// </summary>
[TestFixture]
public sealed class TaskFanTests : PageTest
{
    private const string Documents = "/home/demo/Documents";
    private const string HandTitle = "corrige le pied de page";

    private static readonly LocatorClickOptions Right = new() { Button = MouseButton.Right };

    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Orb => Page.Locator(".orb-frame .orb");

    private ILocator Fan => Page.Locator("[data-fan]");

    private ILocator Surface => Page.Locator("#task-fan");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Window => Page.Locator("[data-dialog-key='Permissions.Confirmation']");

    private ILocator HandPanel => Fan.Locator("[data-fan-panel]").Filter(new() { Has = Page.Locator("[data-fan-title]", new() { HasTextString = HandTitle }) });

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await RestoreAsync();
    }

    [TearDown]
    public async Task Restore()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            // What the host was doing when the scenario stopped: a failure seen only on the build machine
            // (the request window missing, or its panel) could not be explained without it.
            TestContext.Out.WriteLine(await Page.EvaluateAsync<string>(
                """
                () => ['#jarvis-root', '#claude-code', '#confirmation', '#task-fan', '[data-fan]', '.dialog-layer [data-dialog-key]']
                    .flatMap(selector => [...document.querySelectorAll(selector)])
                    .map(element => element.cloneNode(false).outerHTML + ' ' + (element.id === 'jarvis-root' ? '' : element.textContent.trim().slice(0, 1500)))
                    .join('\n')
                """));
        }

        await RestoreAsync();
    }

    [Test]
    public async Task A_right_click_on_the_orb_opens_the_fan_and_never_starts_listening()
    {
        await Orb.ClickAsync(Right);
        await Expect(Fan).ToHaveAttributeAsync("data-fan-phase", "Open");
        await Expect(Surface).ToHaveAttributeAsync("data-shown", "true");
        await Expect(Fan.Locator("[data-fan-count]")).ToBeVisibleAsync();
        await Expect(Fan.Locator("[data-fan-dim]")).ToBeVisibleAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");

        // The orb stays above the fan: a second right click puts the fan away.
        await Orb.ClickAsync(Right);
        await Expect(Fan).ToHaveCountAsync(0);
        await Expect(Surface).ToHaveAttributeAsync("data-shown", "false");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");

        // A left click starts listening at once, then cancels it: nothing answers a double click yet, so no click
        // waits for one (ADR 0027, section 3).
        await Orb.ClickAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Orb.ClickAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
    }

    [Test]
    public async Task A_session_in_a_folder_of_no_project_says_so_on_its_panel_and_the_button_opens_the_projects_page()
    {
        // ADR 0025, section 6: Jarvis never chooses the project; the panel leads to the page where the user does.
        await PressAsync("#hand-session-work");
        await Orb.ClickAsync(Right);
        await Expect(Fan).ToHaveAttributeAsync("data-fan-phase", "Open");
        var ask = HandPanel.Locator("[data-fan-no-project]");
        await Expect(ask).ToBeVisibleAsync();
        await Expect(ask).ToHaveAttributeAsync("title", "/home/demo/projets/kazo-bar");

        await ask.ClickAsync();
        await Expect(Fan).ToHaveCountAsync(0);
        var request = Settings.Locator("[data-project-request='/home/demo/projets/kazo-bar']");
        await Expect(request).ToHaveCountAsync(1);

        // Answered there, the panel no longer asks.
        await request.Locator("[data-project-request-name]").FillAsync("Kazo Bar");
        await request.Locator("[data-project-request-create]").ClickAsync();
        await Expect(request).ToHaveCountAsync(0);
        await CloseDialogsAsync();
        await Orb.ClickAsync(Right);
        await Expect(Fan).ToHaveAttributeAsync("data-fan-phase", "Open");
        await Expect(HandPanel).ToHaveCountAsync(1);
        await Expect(HandPanel.Locator("[data-fan-no-project]")).ToHaveCountAsync(0);
        await Fan.PressAsync("Escape");
        await Expect(Fan).ToHaveCountAsync(0);

        // The host keeps its settings between the tests.
        await OpenSettingsAsync("Projects.Catalog");
        await Settings.Locator("[data-project='kazo-bar'] [data-project-remove]").ClickAsync();
        await Expect(Settings.Locator("[data-project]")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task A_task_has_its_panel_amber_when_it_waits_with_its_sub_tasks_and_the_orb_shows_the_badge()
    {
        await PressAsync("#hand-session-work");
        await Orb.ClickAsync(Right);
        await Expect(Fan).ToHaveAttributeAsync("data-fan-phase", "Open");

        // Where it comes from, its title, its state.
        await Expect(HandPanel).ToHaveAttributeAsync("data-fan-status", "working");
        await Expect(HandPanel).ToHaveAttributeAsync("data-fan-amber", "false");
        await Expect(HandPanel.Locator("[data-fan-from]")).ToHaveTextAsync("Claude Code · kazo-bar · started by hand");
        await Expect(HandPanel.Locator("[data-fan-status-text]")).ToContainTextAsync("Working");

        // A sub-task is listed inside the panel of its task, not as a panel.
        var panels = await Fan.Locator("[data-fan-panel]").CountAsync();
        await PressAsync("#hand-session-subtask");
        await Expect(HandPanel.Locator("[data-fan-subtask]").First).ToContainTextAsync("cherche les pieds de page · Working");
        await Expect(Fan.Locator("[data-fan-panel]")).ToHaveCountAsync(panels);

        // It waits: amber, counted as a decision by the fan and by the badge of the orb. A session started by
        // hand is answered in its own terminal: no button here.
        await PressAsync("#hand-session-wait");
        await Expect(HandPanel).ToHaveAttributeAsync("data-fan-amber", "true");
        await Expect(HandPanel).ToHaveAttributeAsync("data-fan-status", "waiting_approval");
        await Expect(HandPanel.Locator("[data-fan-status-text]")).ToContainTextAsync("Waiting for your permission");
        await Expect(HandPanel.Locator("[data-fan-allow]")).ToHaveCountAsync(0);
        await Expect(Fan.Locator("[data-fan-count-decisions]")).ToContainTextAsync("1 decision waiting");
        await Expect(Orb).ToHaveAttributeAsync("data-decisions", "1");

        // Escape puts the fan away; the badge stays on the orb.
        await Fan.PressAsync("Escape");
        await Expect(Fan).ToHaveCountAsync(0);
        await Expect(Orb).ToHaveAttributeAsync("data-decisions", "1");

        // Its turn ends: nothing waits any more.
        await PressAsync("#hand-session-stop");
        await Expect(Orb).ToHaveAttributeAsync("data-decisions", "0");
    }

    [Test]
    public async Task The_write_of_a_session_started_by_Jarvis_is_allowed_from_its_panel()
    {
        await AllowAsync(Documents);
        await TypeAsync("Ask Claude Code to write the notes");
        await Expect(Window).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await IdleAsync();

        await Orb.ClickAsync(Right);

        // Said apart, so that a failure tells which one is missing: the fan itself, the request, or its buttons.
        await Expect(Fan).ToHaveAttributeAsync("data-fan-phase", "Open");
        await Expect(Window).ToHaveCountAsync(1);
        var panel = Fan.Locator("[data-fan-panel]").Filter(new() { Has = Page.Locator("[data-fan-allow]") });
        await Expect(panel).ToHaveCountAsync(1);
        await Expect(panel).ToHaveAttributeAsync("data-fan-amber", "true");
        await Expect(panel.Locator("[data-fan-from]")).ToHaveTextAsync("Claude Code · Documents · started by Jarvis");
        await Expect(panel.Locator("[data-fan-decision]")).ToHaveTextAsync($"Claude Code wants to write the file {Documents}/NOTES.md in Documents.");

        // The exact detail, on demand.
        await Expect(panel.Locator("[data-fan-detail]")).ToHaveCountAsync(0);
        await panel.Locator("[data-fan-details-toggle]").ClickAsync();
        await Expect(panel.Locator("[data-fan-detail]")).ToContainTextAsync($"\"file_path\":\"{Documents}/NOTES.md\"");

        // A click here is the click of the confirmation window: the request closes, the session goes on.
        await panel.Locator("[data-fan-allow]").ClickAsync();
        await Expect(Window).ToHaveCountAsync(0);
        await Expect(Fan.Locator("[data-fan-allow]")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#claude-code")).ToHaveAttributeAsync("data-last-task-status", "Done", new() { Timeout = 10_000 });
    }

    [Test]
    public async Task The_fan_opens_toward_the_centre_from_any_corner_and_a_click_beside_it_puts_it_away()
    {
        // Bottom right, where the orb sits by default: up and to the left.
        await Orb.ClickAsync(Right);
        await Expect(Fan).ToHaveAttributeAsync("data-fan-horizontal", "left");
        await Expect(Fan).ToHaveAttributeAsync("data-fan-vertical", "up");

        // A click on the darkened desktop, far from the panels.
        await Page.Mouse.ClickAsync(12, 12);
        await Expect(Fan).ToHaveCountAsync(0);

        await PressAsync("#orb-next-corner");
        await PressAsync("#orb-next-corner");
        await Expect(Surface).ToHaveAttributeAsync("data-corner", "top-left");
        await Orb.ClickAsync(Right);
        await Expect(Fan).ToHaveAttributeAsync("data-fan-horizontal", "right");
        await Expect(Fan).ToHaveAttributeAsync("data-fan-vertical", "down");
    }

    private Task PressAsync(string button) => Page.Locator(button).DispatchEventAsync("click");

    private Task IdleAsync() => Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 20_000 });

    private async Task RestoreAsync()
    {
        // The fan, the corner of the orb and the tasks live in the host, not in the page.
        if (await Fan.CountAsync() > 0)
        {
            await Fan.PressAsync("Escape");
            await Expect(Fan).ToHaveCountAsync(0);
        }

        for (var moves = 0; moves < 4 && await Surface.GetAttributeAsync("data-corner") != "bottom-right"; moves++)
        {
            var before = await Surface.GetAttributeAsync("data-corner");
            await PressAsync("#orb-next-corner");
            await Expect(Surface).Not.ToHaveAttributeAsync("data-corner", before!);
        }

        // Closing a confirmation window refuses its request: the session that waited ends.
        await CloseDialogsAsync();
        await PressAsync("#hand-session-stop");
        await IdleAsync();
        await OpenSettingsAsync("ClaudeCode.Launch");
        await Expect(Settings.Locator("[data-launch-install]")).ToHaveAttributeAsync("data-launch-install", "found");
        var stop = Settings.Locator("[data-launch-stop]");
        for (var left = await stop.CountAsync(); left > 0; left--)
        {
            await stop.First.ClickAsync();
            await Expect(stop).ToHaveCountAsync(left - 1);
        }

        await Settings.Locator("[data-settings-page='Tools.Files']").ClickAsync();
        await Expect(Settings.Locator("[data-allowed-folder-input]")).ToBeVisibleAsync();
        var folders = Settings.Locator("[data-allowed-folder-remove]");
        for (var left = await folders.CountAsync(); left > 0; left--)
        {
            await folders.First.ClickAsync();
            await Expect(folders).ToHaveCountAsync(left - 1);
        }

        await CloseDialogsAsync();
        await IdleAsync();
    }

    private async Task AllowAsync(string folder)
    {
        await OpenSettingsAsync("Tools.Files");
        await Settings.Locator("[data-allowed-folder-input]").FillAsync(folder);
        await Settings.Locator("[data-allowed-folder-add]").ClickAsync();
        await Expect(Settings.Locator($"[data-allowed-folder='{folder}']")).ToHaveAttributeAsync("data-folder-usable", "true");
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
        await IdleAsync();
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-conversation");
        await Expect(Conversation.Locator("[data-conversation-window]")).ToBeVisibleAsync();
        await Conversation.Locator("[data-conversation-text]").FillAsync(request);
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");
    }

    private async Task CloseDialogsAsync()
    {
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
