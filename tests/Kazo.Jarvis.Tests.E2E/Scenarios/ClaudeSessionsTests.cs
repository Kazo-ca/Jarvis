using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The "Claude sessions" page on the Web host (work item stage2/11-tasks-and-sessions, ADR 0021, section 6,
/// US2.11 and US2.14): the Claude Code sessions of the computer grouped by project, with folder, state and last
/// activity, those of Claude Desktop included; a session that starts to wait shows without leaving the page; the
/// user can turn the reading off. The registry is simulated: three sessions, two of them clones of the same
/// repository in two folders.
/// </summary>
[TestFixture]
public sealed class ClaudeSessionsTests : PageTest
{
    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator List => Settings.Locator("[data-sessions-list]");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await CloseDialogsAsync();
        await ResetAsync();
    }

    [TearDown]
    public async Task Restore()
    {
        await CloseDialogsAsync();
        await ResetAsync();
        await CloseDialogsAsync();
    }

    [Test]
    public async Task The_sessions_are_listed_by_project_with_folder_state_and_origin()
    {
        await OpenPageAsync();
        await Expect(Settings.Locator(".settings-frame-title")).ToHaveTextAsync("Claude sessions");
        await Expect(Settings.Locator("[data-sessions-enabled]")).ToBeCheckedAsync();
        await Expect(Settings.Locator("[data-sessions-notes] li")).ToHaveCountAsync(3);
        await Expect(Settings.Locator("[data-sessions-notes]")).ToContainTextAsync("It writes nothing there.");
        await Expect(Settings.Locator("[data-sessions-notes]")).ToContainTextAsync("never opens the files of that folder that hold keys");
        await Expect(Settings.Locator("[data-sessions-notes]")).ToContainTextAsync("never answers in their place");

        await Expect(List).ToHaveAttributeAsync("data-sessions-availability", "Read");
        await Expect(List).ToHaveAttributeAsync("data-sessions-count", "3");

        // Two clones of the same repository, in two folders, are one project.
        var projects = List.Locator("[data-sessions-project]");
        await Expect(projects).ToHaveCountAsync(2);
        var bar = List.Locator("[data-sessions-project='github.com/kazo-ca/bar']");
        await Expect(bar.Locator("[data-sessions-project-name]")).ToHaveTextAsync("bar");
        await Expect(bar.Locator("[data-session]")).ToHaveCountAsync(2);
        await Expect(projects.First).ToHaveAttributeAsync("data-sessions-project", "github.com/kazo-ca/bar");
        await Expect(List.Locator("[data-sessions-project='Notes'] [data-session]")).ToHaveCountAsync(1);

        var cli = bar.Locator("[data-session='sess-bar-cli']");
        await Expect(cli).ToHaveAttributeAsync("data-session-state", "Busy");
        await Expect(cli).ToHaveAttributeAsync("data-session-origin", "Manual");
        await Expect(cli).ToHaveAttributeAsync("data-session-topic", "branch:feature/tray-menu");
        await Expect(cli.Locator("[data-session-state-text]")).ToHaveTextAsync("Working");
        await Expect(cli.Locator("[data-session-folder]")).ToHaveTextAsync(@"C:\src\kazo-bar");
        await Expect(cli.Locator("[data-session-meta]")).ToContainTextAsync("Started by hand");
        await Expect(cli.Locator("[data-session-meta]")).ToContainTextAsync("branch:feature/tray-menu");
        await Expect(cli.Locator("[data-session-meta]")).ToContainTextAsync("last activity");

        // A session of code of Claude Desktop is in the same list (US2.14).
        var desktop = bar.Locator("[data-session='sess-bar-desktop']");
        await Expect(desktop).ToHaveAttributeAsync("data-session-origin", "Desktop");
        await Expect(desktop.Locator("[data-session-name]")).ToHaveTextAsync("Relire la PR du menu");
        await Expect(desktop.Locator("[data-session-state-text]")).ToHaveTextAsync("Finished its turn");
        await Expect(desktop.Locator("[data-session-meta]")).ToContainTextAsync("Claude Desktop");
        await Expect(desktop).ToHaveAttributeAsync("data-session-waits", "false");
    }

    [Test]
    public async Task A_session_that_starts_to_wait_shows_without_leaving_the_page()
    {
        await OpenPageAsync();
        var bar = List.Locator("[data-sessions-project='github.com/kazo-ca/bar']");
        await Expect(bar.Locator("[data-session]").First).ToHaveAttributeAsync("data-session", "sess-bar-cli");

        // The registry is read every five seconds: the page follows it.
        await Page.EvaluateAsync("document.getElementById('advance-claude-sessions').click()");
        var desktop = bar.Locator("[data-session='sess-bar-desktop']");
        await Expect(desktop).ToHaveAttributeAsync("data-session-waits", "true", new() { Timeout = 15_000 });
        await Expect(desktop).ToHaveAttributeAsync("data-session-state", "Waiting");
        await Expect(desktop.Locator("[data-session-state-text]")).ToHaveTextAsync("Waiting for you: permission to run Bash");
        await Expect(bar.Locator("[data-session]").First).ToHaveAttributeAsync("data-session", "sess-bar-desktop");

        // Every session ended.
        await Page.EvaluateAsync("document.getElementById('advance-claude-sessions').click()");
        await Expect(List.Locator("[data-sessions-none]")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(List).ToHaveAttributeAsync("data-sessions-count", "0");

        // Claude Code was never there.
        await Page.EvaluateAsync("document.getElementById('advance-claude-sessions').click()");
        await Expect(List.Locator("[data-sessions-no-registry]")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(List).ToHaveAttributeAsync("data-sessions-availability", "NoRegistry");
    }

    [Test]
    public async Task The_user_can_turn_the_reading_off_and_set_its_interval()
    {
        await OpenPageAsync();
        await Expect(List).ToHaveAttributeAsync("data-sessions-count", "3");

        await Settings.Locator("[data-sessions-enabled]").UncheckAsync();
        await Expect(List.Locator("[data-sessions-off]")).ToBeVisibleAsync();
        await Expect(List.Locator("[data-session]")).ToHaveCountAsync(0);
        await Expect(Settings.Locator("[data-sessions-poll]")).ToBeDisabledAsync();

        await Settings.Locator("[data-sessions-enabled]").CheckAsync();
        await Expect(List).ToHaveAttributeAsync("data-sessions-count", "3");

        var poll = Settings.Locator("[data-sessions-poll]");
        await Expect(poll).ToHaveValueAsync("5");
        await poll.FillAsync("1");
        await poll.PressAsync("Tab");
        await Expect(Settings.Locator("[data-sessions-problem]")).ToContainTextAsync("goes from 2 to 60 seconds");
        await poll.FillAsync("10");
        await poll.PressAsync("Tab");
        await Expect(Settings.Locator("[data-sessions-problem]")).ToHaveCountAsync(0);
    }

    private async Task OpenPageAsync()
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-settings");
        await Settings.Locator("[data-settings-page='ClaudeCode.Sessions']").ClickAsync();
        await Expect(List).ToBeVisibleAsync();
    }

    /// <summary>Back to the first step of the simulated registry, the reading on, five seconds: the host keeps them between the tests.</summary>
    private async Task ResetAsync()
    {
        await Page.ClickAsync("#reset-claude-sessions");
        await Expect(Page.Locator("#claude-sessions")).ToHaveAttributeAsync("data-step", "0");
        await OpenPageAsync();
        var enabled = Settings.Locator("[data-sessions-enabled]");
        if (!await enabled.IsCheckedAsync())
        {
            await enabled.CheckAsync();
        }

        var poll = Settings.Locator("[data-sessions-poll]");
        await Expect(poll).ToBeEnabledAsync();
        if (await poll.InputValueAsync() != "5")
        {
            await poll.FillAsync("5");
            await poll.PressAsync("Tab");
        }

        await Expect(List).ToHaveAttributeAsync("data-sessions-count", "3", new() { Timeout = 15_000 });
        await CloseDialogsAsync();
    }

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
