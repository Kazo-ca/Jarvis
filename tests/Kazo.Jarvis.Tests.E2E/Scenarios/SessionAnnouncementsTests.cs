using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The announcements of the Claude Code sessions on the Web host (work item stage2/13-session-announcements;
/// US2.12, US2.13; ADR 0021, section 9; ADR 0024): a session that waits or ends is announced aloud and written in
/// the conversation; during a quiet period it is only written; a folder set to « nothing » is not announced; a
/// session started by hand is announced the same way; « where is Claude on… » and « stay silent for… » go through
/// the tools of the local model.
/// </summary>
[TestFixture]
public sealed class SessionAnnouncementsTests : PageTest
{
    private const string Documents = "/home/demo/Documents";
    private const string HandFolder = "/home/demo/projets/kazo-bar";

    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Window => Page.Locator("[data-dialog-key='Permissions.Confirmation']");

    private ILocator Announcements => Conversation.Locator("[data-conversation-turn][data-turn-input='Announcement']");

    private ILocator Newest => Conversation.Locator("[data-conversation-turn]:not([data-turn-input='Announcement'])").First;

    private ILocator Spoken => Page.Locator("#voice-spoken");

    private ILocator ClaudeCode => Page.Locator("#claude-code");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await CloseDialogsAsync();
        await IdleAsync();
        await ResetAsync();
        await AllowAsync(Documents);
    }

    [TearDown]
    public async Task Restore()
    {
        // Closing a confirmation window refuses its request: the session that waited ends.
        await CloseDialogsAsync();
        await IdleAsync();
        await ResetAsync();
        await CloseDialogsAsync();
        await IdleAsync();
    }

    [Test]
    public async Task A_session_that_waits_then_ends_is_announced_aloud_and_written_in_the_conversation()
    {
        var spoken = await SpokenCountAsync();
        await TypeAsync("Ask Claude Code to write the notes");

        // The wait: said, and written as a line of Jarvis with no request above it.
        await Expect(Window).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Expect(Announcements.First.Locator("[data-conversation-answer]")).ToHaveTextAsync("Claude Code is waiting for your permission on Documents.");
        await Expect(Announcements.First.Locator("[data-conversation-request]")).ToHaveCountAsync(0);
        await Expect(Spoken).ToContainTextAsync("Claude Code is waiting for your permission on Documents.", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-answer]")).ToHaveTextAsync("I am starting Claude Code on Documents.");

        // The end, with what the session said last.
        await Window.Locator("[data-confirmation-approve]").ClickAsync();
        await Expect(Announcements.First.Locator("[data-conversation-answer]")).ToHaveTextAsync(new Regex("^Claude Code is done on Documents\\. (Session simulée|Résumé simulé)"), new() { Timeout = 10_000 });
        await IdleAsync();
        Assert.That(await SpokenCountAsync(), Is.GreaterThanOrEqualTo(spoken + 2));

        // Neither is an exchange: the next request is answered as if they were not there.
        await TypeAsync("Hello again");
        await Expect(Newest.Locator("[data-conversation-request]")).ToHaveTextAsync("Hello again");
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 15_000 });
    }

    [Test]
    public async Task During_a_quiet_period_an_announcement_is_only_written()
    {
        await OpenSettingsAsync("ClaudeCode.Announcements");
        await Expect(Settings.Locator(".settings-frame-title")).ToHaveTextAsync("Announcements");
        await Expect(Settings.Locator("[data-announce-state]")).ToHaveAttributeAsync("data-announce-state", "aloud");
        await Expect(Settings.Locator("[data-announce-limits] li")).ToHaveCountAsync(3);
        await Settings.Locator("[data-announce-period-start]").ClickAsync();
        await Expect(Settings.Locator("[data-announce-state]")).ToHaveAttributeAsync("data-announce-state", "period");

        var spoken = await SpokenCountAsync();
        var written = await CountAsync(Announcements, open: true);
        await TypeAsync("Ask Claude Code to write the notes");
        await Expect(Window).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Expect(Announcements).ToHaveCountAsync(written + 1);
        await Window.Locator("[data-confirmation-refuse]").ClickAsync();
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-status", "Done", new() { Timeout = 10_000 });
        await Expect(Announcements).ToHaveCountAsync(written + 2);
        await Expect(Announcements.First.Locator("[data-conversation-answer]")).ToContainTextAsync("Claude Code is done on Documents.");

        await IdleAsync();
        Assert.That(await SpokenCountAsync(), Is.EqualTo(spoken), "Nothing is said during the quiet period.");

        await OpenSettingsAsync("ClaudeCode.Announcements");
        await Settings.Locator("[data-announce-period-end]").ClickAsync();
        await Expect(Settings.Locator("[data-announce-state]")).ToHaveAttributeAsync("data-announce-state", "aloud");
    }

    [Test]
    public async Task The_page_sets_the_quiet_hours_and_a_folder_set_to_nothing_is_not_announced()
    {
        await OpenSettingsAsync("ClaudeCode.Announcements");
        await Expect(Settings.Locator("[data-announce-no-range]")).ToBeVisibleAsync();

        // A range of every day; one that starts and ends at the same time is refused with the reason.
        await Settings.Locator("[data-announce-range-add]").ClickAsync();
        await Expect(Settings.Locator("[data-announce-range='22:00-07:00']")).ToBeVisibleAsync();
        await Settings.Locator("[data-announce-from]").FillAsync("08:00");
        await Settings.Locator("[data-announce-to]").FillAsync("08:00");
        await Settings.Locator("[data-announce-range-add]").ClickAsync();
        await Expect(Settings.Locator("[data-announce-problem]")).ToBeVisibleAsync();
        await Expect(Settings.Locator("[data-announce-range]")).ToHaveCountAsync(1);
        await Settings.Locator("[data-announce-range-remove]").ClickAsync();
        await Expect(Settings.Locator("[data-announce-no-range]")).ToBeVisibleAsync();

        // Everything is announced by default; « nothing » for a folder is neither said nor written.
        var level = Settings.Locator($"[data-announce-level='{Documents}']");
        await Expect(level).ToHaveValueAsync("All");
        await level.SelectOptionAsync("None");
        await Expect(level).ToHaveValueAsync("None");

        var written = await CountAsync(Announcements, open: true);
        await TypeAsync("Ask Claude Code to write the notes");
        await Expect(Window).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Window.Locator("[data-confirmation-refuse]").ClickAsync();
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-status", "Done", new() { Timeout = 10_000 });
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
        await Expect(Announcements).ToHaveCountAsync(written);
    }

    [Test]
    public async Task A_session_started_by_hand_is_announced_and_the_local_model_can_tell_where_it_stands_and_start_a_silence()
    {
        await OpenConversationAsync();
        await PressAsync("#hand-session-work");
        await PressAsync("#hand-session-wait");
        await Expect(Announcements.First.Locator("[data-conversation-answer]")).ToHaveTextAsync("Claude Code is waiting for your permission on kazo-bar.");
        await Expect(Spoken).ToContainTextAsync("Claude Code is waiting for your permission on kazo-bar.", new() { Timeout = 10_000 });
        await IdleAsync();

        await PressAsync("#hand-session-stop");
        await Expect(Announcements.First.Locator("[data-conversation-answer]")).ToHaveTextAsync("Claude Code is done on kazo-bar. Le pied de page est corrigé.");
        await IdleAsync();

        // « Where is Claude on… »: the state of the session goes to the local model as data.
        await TypeAsync("Where is Claude on Kazo Bar");
        var status = Newest.Locator("[data-conversation-tool='get_claude_sessions']");
        await Expect(status).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 15_000 });
        await Expect(status.Locator("[data-conversation-tool-arguments]")).ToContainTextAsync("Kazo Bar");
        await Expect(status.Locator("[data-conversation-tool-result]")).ToContainTextAsync("\"folder\":\"kazo-bar\"");
        await Expect(status.Locator("[data-conversation-tool-result]")).ToContainTextAsync("\"status\":\"idle\"");
        await Expect(status.Locator("[data-conversation-tool-result]")).ToContainTextAsync("\"started_by\":\"user\"");
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 15_000 });

        // The folder of the session is on the page, and a silence asked to Jarvis shows there.
        await TypeAsync("Stay silent for 2 hours");
        await Expect(Newest.Locator("[data-conversation-tool='start_quiet_period']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 15_000 });
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 15_000 });
        await OpenSettingsAsync("ClaudeCode.Announcements");
        await Expect(Settings.Locator("[data-announce-state]")).ToHaveAttributeAsync("data-announce-state", "period");
        await Expect(Settings.Locator($"[data-announce-level='{HandFolder}']")).ToHaveValueAsync("All");
    }

    /// <summary>Presses a button of the test page while the conversation window covers it.</summary>
    private Task PressAsync(string button) => Page.Locator(button).DispatchEventAsync("click");

    private async Task<int> SpokenCountAsync() =>
        int.Parse(await Spoken.GetAttributeAsync("data-count") ?? "0", CultureInfo.InvariantCulture);

    private async Task<int> CountAsync(ILocator locator, bool open)
    {
        if (open)
        {
            await OpenConversationAsync();
        }

        return await locator.CountAsync();
    }

    private Task IdleAsync() => Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 20_000 });

    /// <summary>The host keeps its state between the tests: no session, no allowed folder, everything announced, no silence.</summary>
    private async Task ResetAsync()
    {
        await OpenSettingsAsync("ClaudeCode.Launch");
        await Expect(Settings.Locator("[data-launch-install]")).ToHaveAttributeAsync("data-launch-install", "found");
        var stop = Settings.Locator("[data-launch-stop]");
        for (var left = await stop.CountAsync(); left > 0; left--)
        {
            await stop.First.ClickAsync();
            await Expect(stop).ToHaveCountAsync(left - 1);
        }

        await Settings.Locator("[data-settings-page='ClaudeCode.Announcements']").ClickAsync();
        await Expect(Settings.Locator("[data-announce-state]")).ToBeVisibleAsync();
        if (await Settings.Locator("[data-announce-period-end]").CountAsync() == 1)
        {
            await Settings.Locator("[data-announce-period-end]").ClickAsync();
            await Expect(Settings.Locator("[data-announce-period-start]")).ToBeVisibleAsync();
        }

        var remove = Settings.Locator("[data-announce-range-remove]");
        for (var left = await remove.CountAsync(); left > 0; left--)
        {
            await remove.First.ClickAsync();
            await Expect(remove).ToHaveCountAsync(left - 1);
        }

        foreach (var level in await Settings.Locator("[data-announce-level]").AllAsync())
        {
            await level.SelectOptionAsync("All");
            await Expect(level).ToHaveValueAsync("All");
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

    private async Task OpenConversationAsync()
    {
        await CloseDialogsAsync(keepConfirmation: true);
        await Page.ClickAsync("#open-conversation");
        await Expect(Conversation.Locator("[data-conversation-window]")).ToBeVisibleAsync();
    }

    private async Task TypeAsync(string request)
    {
        // A request is only taken at rest: an announcement may be being said.
        await IdleAsync();
        await OpenConversationAsync();
        await Conversation.Locator("[data-conversation-text]").FillAsync(request);
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");
    }

    private async Task CloseDialogsAsync(bool keepConfirmation = false)
    {
        // The dialogs live in the host, not in the page: a window left open would cover the buttons of the next test.
        if (await Settings.CountAsync() == 1)
        {
            // The settings window remembers its page; the next test starts on the first one.
            await Settings.Locator("[data-settings-page='Settings.Language']").ClickAsync();
        }

        var dialogs = keepConfirmation ? Page.Locator(".dialog-layer:not(:has([data-confirmation-window]))") : Page.Locator(".dialog-layer");
        for (var open = await dialogs.CountAsync(); open > 0; open--)
        {
            await dialogs.Last.Locator(".dialog-close").ClickAsync();
            await Expect(dialogs).ToHaveCountAsync(open - 1);
        }
    }
}
