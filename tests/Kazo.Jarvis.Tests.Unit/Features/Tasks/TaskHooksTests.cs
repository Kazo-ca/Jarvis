using Kazo.Jarvis.Adapters.Storage;
using Kazo.Jarvis.Features.Tasks.Board;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.LocalServer;
using Kazo.Jarvis.Kernel.Tasks;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.Tasks;

/// <summary>
/// The hooks of a Claude Code session on the board (ADR 0021, sections 5 and 6): a session started by Jarvis
/// moves the task it names; a session started by hand gets a task of source <c>hook</c> at its first hook; a
/// hook answers nothing and decides nothing; its body is a piece of data.
/// </summary>
[TestFixture]
public sealed class TaskHooksTests
{
    private TemporaryFolder folder = null!;
    private FakeTimeProvider time = null!;
    private SqliteDocumentStore store = null!;
    private TaskBoard board = null!;
    private FakeRegistry registry = null!;
    private TaskHooks hooks = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        store = new SqliteDocumentStore(folder.Paths, time);
        board = new TaskBoard(store, time, NullLogger<TaskBoard>.Instance);
        registry = new FakeRegistry();
        hooks = new TaskHooks(board, registry);
    }

    [TearDown]
    public void TearDown()
    {
        hooks.Dispose();
        board.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        folder.Dispose();
    }

    [Test]
    public async Task A_session_of_Jarvis_moves_the_task_it_names_through_its_hooks()
    {
        var task = await board.CreateAsync(new NewWorkTask("Corriger le menu", "github.com/kazo-ca/bar", TaskKeys.Inbox, new TaskOperator(TaskOperator.ClaudeCode), TaskSource.Harness));

        var context = await hooks.HandleAsync(TaskHookKind.Context, Jarvis(task.Id, """{"session_id":"s-1","hook_event_name":"SessionStart"}"""), default);
        var linked = board.Find(task.Id)!;
        await hooks.HandleAsync(TaskHookKind.Start, Jarvis(task.Id, """{"session_id":"s-1","prompt":"Corrige le menu"}"""), default);
        var working = board.Find(task.Id)!;
        await hooks.HandleAsync(TaskHookKind.Approval, Jarvis(task.Id, """{"session_id":"s-1","tool_name":"Bash","tool_input":{"command":"rm -rf /"}}"""), default);
        var waiting = board.Find(task.Id)!;
        await hooks.HandleAsync(TaskHookKind.Idle, Jarvis(task.Id, """{"session_id":"s-1","last_assistant_message":"Le menu est corrigé."}"""), default);
        var idle = board.Find(task.Id)!;

        Assert.Multiple(() =>
        {
            Assert.That(context, Is.EqualTo(LocalResponse.Done), "A hook receives no body: Claude Code goes on.");
            Assert.That(linked.Operator.Session, Is.EqualTo("s-1"));
            Assert.That(linked.Status, Is.EqualTo(WorkTaskStatus.Pending), "SessionStart links, it does not start.");
            Assert.That(working.Status, Is.EqualTo(WorkTaskStatus.Working));
            Assert.That(working.Title, Is.EqualTo("Corriger le menu"), "The title of a task of Jarvis is not the prompt.");
            Assert.That(waiting.Status, Is.EqualTo(WorkTaskStatus.WaitingApproval));
            Assert.That(waiting.WaitsForHuman, Is.True);
            Assert.That(idle.Status, Is.EqualTo(WorkTaskStatus.Idle));
            Assert.That(idle.Summary, Is.EqualTo("Le menu est corrigé."));
            Assert.That(board.Tasks, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task A_task_that_does_not_exist_is_not_created_by_its_name()
    {
        var response = await hooks.HandleAsync(TaskHookKind.Start, Jarvis("task-unknown", """{"session_id":"s-1"}"""), default);

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.EqualTo(LocalResponse.NotFound));
            Assert.That(board.Tasks, Is.Empty);
        });
    }

    [Test]
    public async Task A_session_started_by_hand_gets_a_hook_task_filed_by_its_repository_and_branch()
    {
        registry.Sessions.Add(new ClaudeSession("s-2", 41, @"C:\src\kazo-bar", "tray menu", ClaudeSessionState.Busy, null, null, "cli", "git@github.com:Kazo-ca/Bar.git", "feature/tray"));

        await hooks.HandleAsync(TaskHookKind.Context, User("""{"session_id":"s-2","cwd":"C:\\src\\kazo-bar"}"""), default);
        var started = board.Tasks.Single();
        await hooks.HandleAsync(TaskHookKind.Start, User("""{"session_id":"s-2","prompt":"Ajoute une entrée au menu"}"""), default);
        var working = board.Tasks.Single();

        Assert.Multiple(() =>
        {
            Assert.That(started.Source, Is.EqualTo(TaskSource.Hook));
            Assert.That(started.Status, Is.EqualTo(WorkTaskStatus.Idle), "A session that just started waits for its first request.");
            Assert.That(started.Project, Is.EqualTo("github.com/kazo-ca/bar"));
            Assert.That(started.Topic, Is.EqualTo("branch:feature/tray"));
            Assert.That(started.Title, Is.EqualTo("tray menu"));
            Assert.That(started.Folder, Is.EqualTo(@"C:\src\kazo-bar"));
            Assert.That(started.Operator, Is.EqualTo(new TaskOperator(TaskOperator.ClaudeCode, "s-2")));
            Assert.That(working.Id, Is.EqualTo(started.Id), "The session is found again, not counted twice.");
            Assert.That(working.Status, Is.EqualTo(WorkTaskStatus.Working));
        });
    }

    [Test]
    public async Task Without_the_registry_the_task_is_filed_by_the_folder_and_named_by_the_first_request()
    {
        var prompt = "Explique ce dépôt\nen détail " + new string('x', 200);

        await hooks.HandleAsync(TaskHookKind.Start, User($$"""{"session_id":"s-3","cwd":"/home/benoit/notes","prompt":{{System.Text.Json.JsonSerializer.Serialize(prompt)}} }"""), default);

        var task = board.Tasks.Single();
        Assert.Multiple(() =>
        {
            Assert.That(task.Project, Is.EqualTo("notes"));
            Assert.That(task.Topic, Is.EqualTo(TaskKeys.Inbox));
            Assert.That(task.Title, Is.EqualTo("Explique ce dépôt"), "The first line only.");
            Assert.That(task.Status, Is.EqualTo(WorkTaskStatus.Working));
        });
    }

    [Test]
    public async Task A_notification_of_a_background_task_is_not_a_human_request()
    {
        await hooks.HandleAsync(TaskHookKind.Start, User("""{"session_id":"s-4","cwd":"C:\\src\\jarvis","prompt":"<task-notification>agent 7 ended</task-notification>"}"""), default);

        var task = board.Tasks.Single();
        Assert.Multiple(() =>
        {
            Assert.That(task.Title, Is.EqualTo("jarvis"), "The notification never becomes a title.");
            Assert.That(task.Status, Is.EqualTo(WorkTaskStatus.Working), "The session works again all the same.");
        });
    }

    [Test]
    public async Task The_token_of_the_users_hooks_cannot_name_a_task()
    {
        var owned = await board.CreateAsync(new NewWorkTask("Tâche de Jarvis", "p", TaskKeys.Inbox, new TaskOperator(TaskOperator.ClaudeCode), TaskSource.Harness));
        var request = new LocalRequest(HookPaths.Idle, LocalCaller.UserHook, Headers(owned.Id), """{"session_id":"s-5","cwd":"C:\\x","last_assistant_message":"fini"}""");

        await hooks.HandleAsync(TaskHookKind.Idle, request, default);

        Assert.Multiple(() =>
        {
            Assert.That(board.Find(owned.Id)!.Status, Is.EqualTo(WorkTaskStatus.Pending), "The named task did not move.");
            Assert.That(board.Tasks, Has.Count.EqualTo(2));
            Assert.That(board.FindBySession(TaskOperator.ClaudeCode, "s-5")!.Source, Is.EqualTo(TaskSource.Hook));
        });
    }

    [Test]
    public async Task A_subagent_that_stops_becomes_a_sub_task_done()
    {
        await hooks.HandleAsync(TaskHookKind.Start, User("""{"session_id":"s-6","cwd":"C:\\src\\jarvis","prompt":"Cherche"}"""), default);
        var parent = board.Tasks.Single();

        await hooks.HandleAsync(TaskHookKind.SubtaskIdle, User("""{"session_id":"s-6","agent_id":"a-1","agent_type":"Explore","last_assistant_message":"Trouvé."}"""), default);
        await hooks.HandleAsync(TaskHookKind.SubtaskIdle, User("""{"session_id":"s-6","agent_id":"a-1","agent_type":"Explore","last_assistant_message":"Encore."}"""), default);

        var subtask = board.Tasks.Single(task => task.ParentId is not null);
        Assert.Multiple(() =>
        {
            Assert.That(board.Tasks, Has.Count.EqualTo(2), "The same agent is one sub-task.");
            Assert.That(subtask.ParentId, Is.EqualTo(parent.Id));
            Assert.That(subtask.Title, Is.EqualTo("Explore"));
            Assert.That(subtask.Status, Is.EqualTo(WorkTaskStatus.Done));
            Assert.That(subtask.Summary, Is.EqualTo("Trouvé."), "An ended sub-task no longer moves.");
            Assert.That(board.Find(parent.Id)!.Status, Is.EqualTo(WorkTaskStatus.Working), "The end of a sub-agent does not end the session.");
        });
    }

    [TestCase("")]
    [TestCase("not json")]
    [TestCase("[1,2]")]
    [TestCase("""{"prompt":"no session"}""")]
    [TestCase("""{"session_id":42}""")]
    public async Task A_body_that_cannot_be_used_changes_nothing(string body)
    {
        var response = await hooks.HandleAsync(TaskHookKind.Start, User(body), default);

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.EqualTo(LocalResponse.BadRequest));
            Assert.That(board.Tasks, Is.Empty);
        });
    }

    [Test]
    public async Task Hooks_that_arrive_together_create_one_task()
    {
        var all = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(async () => await hooks.HandleAsync(TaskHookKind.Start, User("""{"session_id":"s-7","cwd":"C:\\x","prompt":"Go"}"""), default)))
            .ToList();

        await Task.WhenAll(all);

        Assert.That(board.Tasks, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task A_session_whose_task_ended_gets_a_new_task()
    {
        await hooks.HandleAsync(TaskHookKind.Start, User("""{"session_id":"s-8","cwd":"C:\\x","prompt":"Un"}"""), default);
        var first = board.Tasks.Single();
        await board.ChangeAsync(first.Id, new TaskChange(WorkTaskStatus.Abandoned));
        time.Advance(TimeSpan.FromMinutes(1));

        await hooks.HandleAsync(TaskHookKind.Start, User("""{"session_id":"s-8","cwd":"C:\\x","prompt":"Deux"}"""), default);

        Assert.Multiple(() =>
        {
            Assert.That(board.Tasks, Has.Count.EqualTo(2));
            Assert.That(board.Find(first.Id)!.Status, Is.EqualTo(WorkTaskStatus.Abandoned));
            Assert.That(board.FindBySession(TaskOperator.ClaudeCode, "s-8")!.Title, Is.EqualTo("Deux"));
        });
    }

    [Test]
    public void The_slice_gives_the_five_routes_of_the_specification_once()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Kazo.Jarvis.Kernel.Storage.IDocumentStore>(store);
        services.AddLogging();
        services.AddTaskBoardSlice();
        services.AddTaskBoardSlice();
        using var provider = services.BuildServiceProvider();

        var paths = provider.GetServices<ILocalRoute>().Select(route => route.Path).ToList();

        Assert.That(paths, Is.EquivalentTo(new[] { "/hook/task.context", "/hook/task.start", "/hook/approval.requested", "/hook/task.idle", "/hook/subtask.idle" }));
    }

    private static LocalRequest Jarvis(string task, string body) => new(HookPaths.Start, LocalCaller.JarvisSession, Headers(task), body);

    private static LocalRequest User(string body) => new(HookPaths.Start, LocalCaller.UserHook, Headers(null), body);

    private static Dictionary<string, string> Headers(string? task)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (task is not null)
        {
            headers[LocalRequest.TaskHeader] = task;
        }

        return headers;
    }

    private sealed class FakeRegistry : IClaudeSessionRegistry
    {
        public List<ClaudeSession> Sessions { get; } = [];

        public ValueTask<ClaudeSessionListing> ReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ClaudeSessionListing(true, [.. Sessions]));
    }
}
