using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The projects view on the Web host (work item stage2/24-projects-view; ADR 0026; ADR 0027): a double click on the
/// orb opens it; a session that belongs to no project shows in « Général » with its task, its sub-tasks and its
/// thread; a task that waits is listed in « À traiter »; once its folder has a project, the session moves there.
/// </summary>
[TestFixture]
public sealed class ProjectsViewTests : PageTest
{
    private const string Folder = "/home/demo/projets/kazo-bar";
    private const string HandTitle = "corrige le pied de page";
    private const string General = "_general";

    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Orb => Page.Locator(".orb-frame .orb");

    private ILocator View => Page.Locator("[data-dialog-key='Projects.View'] [data-projects-view]");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator HandStep => View.Locator("[data-step]").Filter(new() { HasTextString = HandTitle });

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await RestoreAsync();
    }

    [TearDown]
    public Task Restore() => RestoreAsync();

    [Test]
    public async Task A_double_click_on_the_orb_opens_the_view_and_a_single_click_still_listens()
    {
        await Orb.DblClickAsync();
        await Expect(View).ToHaveAttributeAsync("data-projects-rail", "_all");
        await Expect(View.Locator($"[data-project-card='{General}']")).ToHaveCountAsync(1);
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");

        // The built-in project is always in the rail, and "+" leads to the page where a project is made.
        await View.Locator("[data-rail-new]").ClickAsync();
        await Expect(Settings.Locator("[data-settings-page='Projects.Catalog']")).ToHaveAttributeAsync("aria-current", "page");
        await CloseDialogsAsync();

        await Orb.ClickAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Orb.ClickAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
    }

    [Test]
    public async Task A_session_of_no_project_shows_in_the_built_in_project_with_its_task_its_sub_tasks_and_its_thread()
    {
        await PressAsync("#hand-session-work");
        await Orb.DblClickAsync();

        // « Tous les projets »: the folder asks for its project, the card of « Général » names what runs.
        await Expect(View.Locator($"[data-folder-card='{Folder}']")).ToHaveCountAsync(1);
        // Sessions of other scenarios may still run in the host: only the one of this scenario is looked for.
        await Expect(View.Locator($"[data-project-card='{General}'] [data-card-running]").Filter(new() { HasTextString = HandTitle })).ToHaveCountAsync(1);

        await View.Locator($"[data-rail='{General}']").ClickAsync();
        await Expect(View.Locator($"[data-project-panel='{General}']")).ToHaveCountAsync(1);
        await View.Locator("[data-subject='kazo-bar']").ClickAsync();
        await Expect(View.Locator("[data-subject-title]")).ToHaveTextAsync("kazo-bar");
        await Expect(HandStep).ToHaveAttributeAsync("data-step-kind", "Active");
        await Expect(HandStep).ToHaveAttributeAsync("data-step-amber", "false");
        await Expect(View.Locator("[data-thread-line='Opened']").First).ToContainTextAsync(HandTitle);
        await Expect(View.Locator("[data-projects-find]")).ToHaveCountAsync(1);

        // Its sub-task is shown on the line of the task in progress, never as a task of the subject.
        await PressAsync("#hand-session-subtask");
        var label = await HandStep.GetAttributeAsync("data-step");
        await Expect(View.Locator($"[data-step-subtasks='{label}']")).ToContainTextAsync("cherche les pieds de page");
        await Expect(View.Locator("[data-step]").Filter(new() { HasTextString = "cherche les pieds de page" })).ToHaveCountAsync(0);

        // The thread of the task opens on the right, in place of the search, and closes.
        await HandStep.ClickAsync();
        var thread = View.Locator("[data-task-thread]");
        await Expect(thread.Locator("[data-task-status]")).ToContainTextAsync("Claude Code");
        await Expect(thread.Locator("[data-task-subtasks]")).ToContainTextAsync("cherche les pieds de page");
        await Expect(View.Locator("[data-projects-find]")).ToHaveCountAsync(0);
        await thread.Locator("[data-task-close]").ClickAsync();
        await Expect(thread).ToHaveCountAsync(0);

        // The search finds the task by a word of its title.
        await View.Locator("#pv-find").FillAsync("pied de page");
        await Expect(View.Locator("[data-find-hit]").First).ToContainTextAsync(HandTitle);
        await View.Locator("#pv-find").FillAsync("zzz");
        await Expect(View.Locator("[data-find-hit]")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task A_task_that_waits_is_amber_listed_in_what_to_handle_and_one_click_opens_its_thread()
    {
        await PressAsync("#hand-session-work");
        await Orb.DblClickAsync();
        var attention = View.Locator("[data-rail='_attention']");
        await Expect(attention.Locator("[data-rail-badge]")).ToHaveCountAsync(0);

        await PressAsync("#hand-session-wait");
        await Expect(attention.Locator("[data-rail-badge]")).ToHaveTextAsync("1");
        await Expect(View.Locator($"[data-rail='{General}'] [data-rail-badge]")).ToHaveTextAsync("1");

        await attention.ClickAsync();
        var item = View.Locator("[data-attention]").Filter(new() { HasTextString = HandTitle });
        await Expect(item).ToHaveCountAsync(1);
        await item.ClickAsync();
        await Expect(View).ToHaveAttributeAsync("data-projects-rail", General);
        await Expect(View.Locator("[data-task-thread]")).ToContainTextAsync(HandTitle);
        await Expect(HandStep).ToHaveAttributeAsync("data-step-amber", "true");
        await Expect(View.Locator("[data-thread-line='Waiting']")).ToHaveCountAsync(1);

        // It no longer waits: nothing is left to handle.
        await PressAsync("#hand-session-work");
        await Expect(attention.Locator("[data-rail-badge]")).ToHaveCountAsync(0);
        await attention.ClickAsync();
        await Expect(View.Locator("[data-projects-nothing]")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Once_its_folder_has_a_project_the_session_is_shown_in_that_project()
    {
        await PressAsync("#hand-session-work");
        await Orb.DblClickAsync();
        await View.Locator($"[data-folder-card='{Folder}']").ClickAsync();
        var request = Settings.Locator($"[data-project-request='{Folder}']");
        await request.Locator("[data-project-request-name]").FillAsync("Kazo Bar");
        await request.Locator("[data-project-request-create]").ClickAsync();
        await Expect(request).ToHaveCountAsync(0);
        await Settings.Locator(".dialog-close").ClickAsync();

        // The view followed the catalog without being opened again.
        await Expect(View.Locator($"[data-folder-card='{Folder}']")).ToHaveCountAsync(0);
        var rail = View.Locator("[data-rail='kazo-bar']");
        await Expect(rail).ToHaveTextAsync("KB");
        await rail.ClickAsync();
        await Expect(View.Locator("[data-project-panel='kazo-bar'] [data-project-folder]")).ToHaveTextAsync(Folder);
        await Expect(HandStep).ToHaveCountAsync(1);

        // Closed and opened again, the view is where the user left it.
        await CloseDialogsAsync();
        await Orb.DblClickAsync();
        await Expect(View).ToHaveAttributeAsync("data-projects-rail", "kazo-bar");
    }

    [Test]
    public async Task A_message_written_in_the_thread_of_a_subject_is_answered_there_and_T1_answers_a_task()
    {
        await PressAsync("#hand-session-work");
        await Orb.DblClickAsync();
        await View.Locator($"[data-rail='{General}']").ClickAsync();
        await View.Locator("[data-subject='kazo-bar']").ClickAsync();
        var text = View.Locator("[data-thread-text]");
        var send = View.Locator("[data-thread-send]");
        var requests = View.Locator("[data-thread-message='request']");
        var answers = View.Locator("[data-thread-message='answer']");
        await Expect(send).ToBeDisabledAsync();
        var before = await requests.CountAsync();

        // Written to the subject: the request and its answer take their place in the thread.
        await text.FillAsync("où en est le pied de page ?");
        await Expect(send).ToBeEnabledAsync();
        await send.ClickAsync();
        await Expect(requests).ToHaveCountAsync(before + 1);
        await Expect(requests.Last).ToContainTextAsync("où en est le pied de page ?");
        await Expect(answers.Last).ToHaveAttributeAsync("data-thread-pending", "false", new() { Timeout = 20_000 });
        await Expect(answers.Last).Not.ToBeEmptyAsync();
        await Expect(text).ToHaveValueAsync(string.Empty);
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 20_000 });

        // A task the subject does not have is refused, and what was written is kept.
        await text.FillAsync("T99> non");
        await send.ClickAsync();
        await Expect(View.Locator("[data-thread-problem]")).ToContainTextAsync("T99");
        await Expect(text).ToHaveValueAsync("T99> non");
        await Expect(requests).ToHaveCountAsync(before + 1);

        // « T1> … » answers the task: the message carries its tag, without the prefix, and shows in the thread of the task.
        var label = await HandStep.GetAttributeAsync("data-step");
        await text.FillAsync($"{label}> oui, on fait comme ça");
        await send.ClickAsync();
        await Expect(View.Locator("[data-thread-problem]")).ToHaveCountAsync(0);
        await Expect(requests).ToHaveCountAsync(before + 2);
        await Expect(requests.Last.Locator("[data-thread-tag]")).ToHaveTextAsync(label!);
        await Expect(requests.Last).ToContainTextAsync("oui, on fait comme ça");
        await Expect(requests.Last).Not.ToContainTextAsync(">");
        await Expect(answers.Last).ToHaveAttributeAsync("data-thread-pending", "false", new() { Timeout = 20_000 });
        await requests.Last.Locator("[data-thread-tag]").ClickAsync();
        await Expect(View.Locator("[data-task-thread] [data-task-messages]")).ToContainTextAsync("oui, on fait comme ça");
    }

    private Task PressAsync(string button) => Page.Locator(button).DispatchEventAsync("click");

    private async Task RestoreAsync()
    {
        // The tasks, the projects and what the view shows live in the host, not in the page.
        await CloseDialogsAsync();
        await PressAsync("#hand-session-stop");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 20_000 });
        await Page.ClickAsync("#open-settings");
        await Settings.Locator("[data-settings-page='Projects.Catalog']").ClickAsync();
        await Expect(Settings.Locator("[data-project-add-name]")).ToBeVisibleAsync();
        var projects = Settings.Locator("[data-project-remove]");
        for (var left = await projects.CountAsync(); left > 0; left--)
        {
            await projects.First.ClickAsync();
            await Expect(projects).ToHaveCountAsync(left - 1);
        }

        await CloseDialogsAsync();
        if (await View.CountAsync() == 0)
        {
            await Orb.DblClickAsync();
        }

        await View.Locator("[data-rail='_all']").ClickAsync();
        await CloseDialogsAsync();
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
