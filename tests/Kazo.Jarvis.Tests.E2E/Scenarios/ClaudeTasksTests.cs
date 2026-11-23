using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The work handed to Claude Code on the Web host (work item stage2/12-claude-code-tasks; US2.9, US2.10; ADR 0021):
/// « Ask Claude Code to… » starts a simulated session on an allowed folder after saying which; the write of the
/// session waits for a human answer in the same confirmation window as a tool of Jarvis; a path outside the folder
/// is refused without a question; a command that never asked stops the session; the page stops a session, sets
/// the cap and marks a folder trusted; the cost of a session is shown apart on the spending page.
/// </summary>
[TestFixture]
public sealed class ClaudeTasksTests : PageTest
{
    private const string Documents = "/home/demo/Documents";
    private const string Recipes = "/home/demo/Documents/recettes";

    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Window => Page.Locator("[data-dialog-key='Permissions.Confirmation']");

    // The announcements of the sessions (stage2/13) are lines of the conversation too: the newest request is not one.
    private ILocator Newest => Conversation.Locator("[data-conversation-turn]:not([data-turn-input='Announcement'])").First;

    private ILocator ClaudeCode => Page.Locator("#claude-code");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await ResetAsync();
        await AllowAsync(Documents);
    }

    [TearDown]
    public async Task Restore()
    {
        // Closing a confirmation window refuses its request: the session that waited ends.
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await ResetAsync();
        await CloseDialogsAsync();
    }

    [Test]
    public async Task The_page_says_what_a_session_may_do_and_shows_its_settings()
    {
        await OpenSettingsAsync("ClaudeCode.Launch");

        await Expect(Settings.Locator(".settings-frame-title")).ToHaveTextAsync("Claude Code tasks");
        await Expect(Settings.Locator("[data-launch-install]")).ToHaveAttributeAsync("data-launch-install", "found");
        await Expect(Settings.Locator("[data-launch-notes] li")).ToHaveCountAsync(4);
        await Expect(Settings.Locator("[data-launch-notes]")).ToContainTextAsync("every write and every command", new() { IgnoreCase = true });
        await Expect(Settings.Locator("[data-launch-no-session]")).ToBeVisibleAsync();
        await Expect(Settings.Locator("[data-launch-budget]")).ToHaveValueAsync("2");
        await Expect(Settings.Locator("[data-launch-most]")).ToHaveValueAsync("3");
        await Expect(Settings.Locator($"[data-launch-trusted='{Documents}']")).Not.ToBeCheckedAsync();
        await Expect(Settings.Locator("[data-launch-trusted-warning]")).ToContainTextAsync("runs the hooks of its repository without asking");

        // A cap out of bounds is refused, with the reason.
        await Settings.Locator("[data-launch-budget]").FillAsync("5000");
        await Settings.Locator("[data-launch-budget]").BlurAsync();
        await Expect(Settings.Locator("[data-launch-problem]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Ask_Claude_Code_starts_a_session_whose_write_waits_for_a_click_and_is_done_once_approved()
    {
        var launches = await CountAsync();
        await OpenSettingsAsync("Llm.Spend");
        var counted = await Settings.Locator("[data-spend-count]").GetAttributeAsync("data-spend-count");
        await TypeAsync("Ask Claude Code to write the notes");

        // The folder is said before the launch, and the turn ends there: the session works on its own.
        await Expect(Newest.Locator("[data-conversation-answer]")).ToHaveTextAsync("I am starting Claude Code on Documents.");
        await Expect(Newest.Locator("[data-conversation-handoff]")).ToHaveAttributeAsync("data-handoff-forced", "true");
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-launches", (launches + 1).ToString(CultureInfo.InvariantCulture));
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-folder", Documents);
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-trusted", "false");
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-budget", "2");
        // The first message ends with the request word for word; a summary of the earlier conversation, when the
        // host has one from other tests, comes before it as a context.
        await Expect(ClaudeCode).ToHaveTextAsync(new Regex("(^|Request:\\s+)to write the notes$"));
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 10_000 });

        // Its write arrives as a request of the session: the folder, the tool, the exact path.
        await Expect(Window).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Expect(Window.Locator("[data-confirmation-tool]")).ToHaveTextAsync("Write");
        await Expect(Window.Locator("[data-confirmation-summary]")).ToHaveTextAsync($"Claude Code wants to write the file {Documents}/NOTES.md in Documents.");
        await Expect(Window.Locator("[data-confirmation-detail]")).ToContainTextAsync($"\"file_path\":\"{Documents}/NOTES.md\"");
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-status", "WaitingApproval");

        await Window.Locator("[data-confirmation-approve]").ClickAsync();
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-status", "Done", new() { Timeout = 10_000 });
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-summary", new Regex("NOTES\\.md est écrit"));
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-end", "done");
        await ExpectLoggedAsync("Write", allowed: true, cause: string.Empty);

        // Its cost is shown apart: Claude Code is paid by subscription unless the user says otherwise.
        await OpenSettingsAsync("Llm.Spend");
        await Expect(Settings.Locator("[data-spend-claude-subscription]")).ToBeCheckedAsync();
        await Expect(Settings.Locator("[data-spend-claude-apart]")).Not.ToHaveAttributeAsync("data-spend-claude-apart", "0");
        await Expect(Settings.Locator("[data-spend-claude-entry='apart']").First).ToContainTextAsync("Claude Code");
        await Expect(Settings.Locator("[data-spend-count]")).ToHaveAttributeAsync("data-spend-count", counted!);
    }

    [Test]
    public async Task A_refused_write_is_told_to_the_session_which_goes_on_without_it()
    {
        await TypeAsync("Ask Claude Code to write the notes");

        await Expect(Window).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Window.Locator("[data-confirmation-refuse]").ClickAsync();

        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-status", "Done", new() { Timeout = 10_000 });
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-summary", new Regex("a été refusée \\(The user refused\\.\\)"));
        await ExpectLoggedAsync("Write", allowed: false, cause: "Refused");
    }

    [Test]
    public async Task A_write_outside_the_folder_of_the_session_is_refused_without_a_question()
    {
        await TypeAsync("Ask Claude Code to write outside its folder");

        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-status", "Done", new() { Timeout = 10_000 });
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-summary", new Regex("secret\\.txt a été refusée \\(Jarvis refuses"));
        await Expect(Window).ToHaveCountAsync(0);
        await ExpectLoggedAsync("Write", allowed: false, cause: "Forbidden");
    }

    [Test]
    public async Task A_command_that_ran_without_asking_stops_the_session()
    {
        await TypeAsync("Ask Claude Code to push without asking");

        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-status", "Blocked", new() { Timeout = 10_000 });
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-summary", "Stopped by Jarvis: the tool Bash ran without asking for its permission.");
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-end", "stopped");
    }

    [Test]
    public async Task The_page_lists_a_running_session_and_stops_it()
    {
        await TypeAsync("Ask Claude Code to wait for me");
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-status", "Working", new() { Timeout = 10_000 });

        await OpenSettingsAsync("ClaudeCode.Launch");
        await Expect(Settings.Locator("[data-launch-sessions]")).ToHaveAttributeAsync("data-launch-sessions", "1");
        await Expect(Settings.Locator("[data-launch-session]")).ToContainTextAsync("Documents");
        await Settings.Locator("[data-launch-stop]").ClickAsync();

        await Expect(Settings.Locator("[data-launch-no-session]")).ToBeVisibleAsync();
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-status", "Abandoned");
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-summary", "Stopped by the user.");
    }

    [Test]
    public async Task With_two_folders_Jarvis_asks_which_and_the_next_request_answers()
    {
        await AllowAsync(Recipes);
        var launches = await CountAsync();

        await TypeAsync("Ask Claude Code to wait for me");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToHaveTextAsync("On which folder? Documents or recettes.");
        await Expect(Newest.Locator("[data-conversation-handoff]")).ToHaveAttributeAsync("data-handoff-failure", "NotStarted");
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-launches", launches.ToString(CultureInfo.InvariantCulture));

        await TypeAsync("Recettes");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToHaveTextAsync("I am starting Claude Code on recettes.");
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-folder", Recipes);
        await Expect(ClaudeCode).ToHaveTextAsync(new Regex("(^|Request:\\s+)to wait for me$"));

        // A request that names its folder needs no question.
        await TypeAsync("Ask Claude Code to wait in Documents");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToHaveTextAsync("I am starting Claude Code on Documents.");
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-launches", (launches + 2).ToString(CultureInfo.InvariantCulture));
    }

    [Test]
    public async Task A_trusted_folder_and_the_cap_of_a_session_go_to_the_session_and_the_sessions_at_once_are_bounded()
    {
        await OpenSettingsAsync("ClaudeCode.Launch");
        await Settings.Locator($"[data-launch-trusted='{Documents}']").CheckAsync();
        await Settings.Locator("[data-launch-budget]").FillAsync("0.5");
        await Settings.Locator("[data-launch-budget]").BlurAsync();
        await Settings.Locator("[data-launch-most]").FillAsync("1");
        await Settings.Locator("[data-launch-most]").BlurAsync();
        await Expect(Settings.Locator("[data-launch-problem]")).ToHaveCountAsync(0);

        await TypeAsync("Ask Claude Code to wait for me");
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-status", "Working", new() { Timeout = 10_000 });
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-trusted", "true");
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-budget", "0.5");

        await TypeAsync("Ask Claude Code to wait again");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("1 Claude Code sessions are already running");
    }

    [Test]
    public async Task An_answer_written_to_the_task_in_the_projects_view_resumes_the_session_that_is_over()
    {
        var launches = await CountAsync();
        await TypeAsync("Ask Claude Code to write the notes");
        await Expect(Window).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Window.Locator("[data-confirmation-approve]").ClickAsync();
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-status", "Done", new() { Timeout = 10_000 });
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await CloseDialogsAsync();

        // The projects view (ADR 0026): the session is a task of the subject of its folder, in « Général ».
        await Page.Locator(".orb-frame .orb").DblClickAsync();
        var view = Page.Locator("[data-dialog-key='Projects.View'] [data-projects-view]");
        await view.Locator("[data-rail='_general']").ClickAsync();
        await view.Locator("[data-subject]").Filter(new() { HasTextString = "Documents" }).First.ClickAsync();
        // Sessions of other scenarios stay on the board: the task of this one is the newest of its title.
        var step = view.Locator("[data-step]").Filter(new() { HasTextString = "write the notes" }).Last;
        await Expect(step).ToHaveAttributeAsync("data-step-kind", "Done");
        var label = await step.GetAttributeAsync("data-step");

        // « T1> … » hands the answer to the session, which resumes: the same task works again, then is done.
        await view.Locator("[data-thread-text]").FillAsync($"{label}> yes, go on");
        await view.Locator("[data-thread-send]").ClickAsync();
        var answer = view.Locator("[data-thread-message='answer']").Last;
        await Expect(answer).ToContainTextAsync("handing your answer to the Claude Code session of Documents", new() { Timeout = 15_000 });
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-launches", (launches + 2).ToString(CultureInfo.InvariantCulture));
        await Expect(ClaudeCode).ToHaveTextAsync("yes, go on");
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-summary", new Regex("Session simulée reprise.*yes, go on"), new() { Timeout = 10_000 });
        await Expect(ClaudeCode).ToHaveAttributeAsync("data-last-task-status", "Done");
        await Expect(step).ToHaveAttributeAsync("data-step", label!);
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });

        // The view remembers where it was: the next scenario finds it on all the projects.
        await view.Locator("[data-rail='_all']").ClickAsync();
    }

    private async Task<int> CountAsync() =>
        int.Parse(await ClaudeCode.GetAttributeAsync("data-launches") ?? "0", CultureInfo.InvariantCulture);

    /// <summary>The host keeps its state between the tests: no session, no allowed folder, the settings of a first start.</summary>
    private async Task ResetAsync()
    {
        await OpenSettingsAsync("ClaudeCode.Launch");
        // The page is there before its sessions and folders are counted.
        await Expect(Settings.Locator("[data-launch-install]")).ToHaveAttributeAsync("data-launch-install", "found");
        var stop = Settings.Locator("[data-launch-stop]");
        for (var left = await stop.CountAsync(); left > 0; left--)
        {
            await stop.First.ClickAsync();
            await Expect(stop).ToHaveCountAsync(left - 1);
        }

        foreach (var trusted in await Settings.Locator("[data-launch-trusted]").AllAsync())
        {
            await trusted.UncheckAsync();
        }

        await Expect(Settings.Locator("[data-launch-trusted]:checked")).ToHaveCountAsync(0);

        await Settings.Locator("[data-launch-budget]").FillAsync("2");
        await Settings.Locator("[data-launch-budget]").BlurAsync();
        await Settings.Locator("[data-launch-most]").FillAsync("3");
        await Settings.Locator("[data-launch-most]").BlurAsync();

        await Settings.Locator("[data-settings-page='Tools.Files']").ClickAsync();
        await Expect(Settings.Locator("[data-allowed-folder-input]")).ToBeVisibleAsync();
        var remove = Settings.Locator("[data-allowed-folder-remove]");
        for (var left = await remove.CountAsync(); left > 0; left--)
        {
            await remove.First.ClickAsync();
            await Expect(remove).ToHaveCountAsync(left - 1);
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

    private async Task TypeAsync(string request)
    {
        await CloseDialogsAsync(keepConfirmation: true);

        // A request is only taken at rest: an announcement of a session may be being said.
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await Page.ClickAsync("#open-conversation");
        await Expect(Conversation.Locator("[data-conversation-window]")).ToBeVisibleAsync();
        await Conversation.Locator("[data-conversation-text]").FillAsync(request);
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");
    }

    /// <summary>The newest decision of the permission log is the one described.</summary>
    private async Task ExpectLoggedAsync(string tool, bool allowed, string cause)
    {
        await OpenSettingsAsync("Permissions.Decision");
        var newest = Settings.Locator("[data-decision-log] > li").First;
        await Expect(newest).ToHaveAttributeAsync("data-decision-tool", tool);
        await Expect(newest).ToHaveAttributeAsync("data-decision-allowed", allowed ? "true" : "false");
        await Expect(newest).ToHaveAttributeAsync("data-decision-cause", cause);
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
