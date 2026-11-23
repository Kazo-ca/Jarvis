using Kazo.Jarvis.Adapters.Storage;
using Kazo.Jarvis.Features.Tasks.Board;
using Kazo.Jarvis.Kernel.Tasks;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.Tasks;

/// <summary>
/// The board of the tasks (ADR 0013, section 6; ADR 0021, section 7): a task is created <c>pending</c>, moves
/// through the nine statuses, no longer moves once it ended, has sub-tasks on one level only, and is found again
/// in <c>jarvis.db</c> after a restart.
/// </summary>
[TestFixture]
public sealed class TaskBoardTests
{
    private static readonly NewWorkTask Asked = new(
        "Corriger le menu de la barre",
        "github.com/kazo-ca/bar",
        "branch:feature/tray-menu",
        new TaskOperator(TaskOperator.ClaudeCode),
        TaskSource.Harness,
        @"C:\src\kazo-bar");

    private TemporaryFolder folder = null!;
    private FakeTimeProvider time = null!;
    private SqliteDocumentStore store = null!;
    private TaskBoard board = null!;
    private int changes;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        changes = 0;
        store = new SqliteDocumentStore(folder.Paths, time);
        board = Board();
    }

    [TearDown]
    public void TearDown()
    {
        board.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        folder.Dispose();
    }

    [Test]
    public async Task A_task_is_created_pending_with_the_shape_of_the_specification()
    {
        var task = await board.CreateAsync(Asked);

        Assert.Multiple(() =>
        {
            Assert.That(task.Id, Does.StartWith("task-"));
            Assert.That(task.ParentId, Is.Null);
            Assert.That(task.Status, Is.EqualTo(WorkTaskStatus.Pending), "Pending until its first hook (ADR 0021, section 5).");
            Assert.That(task.Project, Is.EqualTo("github.com/kazo-ca/bar"));
            Assert.That(task.Topic, Is.EqualTo("branch:feature/tray-menu"));
            Assert.That(task.Title, Is.EqualTo("Corriger le menu de la barre"));
            Assert.That(task.Summary, Is.Null);
            Assert.That(task.Operator, Is.EqualTo(new TaskOperator("claude-code")));
            Assert.That(task.Source, Is.EqualTo(TaskSource.Harness));
            Assert.That(task.Folder, Is.EqualTo(@"C:\src\kazo-bar"));
            Assert.That((task.CreatedAt, task.UpdatedAt), Is.EqualTo((time.GetUtcNow(), time.GetUtcNow())));
            Assert.That(board.Find(task.Id), Is.EqualTo(task));
            Assert.That(board.Tasks, Is.EqualTo(new[] { task }));
            Assert.That(changes, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task A_task_moves_and_keeps_what_does_not_change()
    {
        var task = await board.CreateAsync(Asked);
        time.Advance(TimeSpan.FromMinutes(1));

        var working = await board.ChangeAsync(task.Id, new TaskChange(WorkTaskStatus.Working, Session: "sess-1"));
        time.Advance(TimeSpan.FromMinutes(1));
        var waiting = await board.ChangeAsync(task.Id, new TaskChange(WorkTaskStatus.WaitingApproval, "Bash : dotnet test"));

        Assert.Multiple(() =>
        {
            Assert.That(working!.Status, Is.EqualTo(WorkTaskStatus.Working));
            Assert.That(working.Operator.Session, Is.EqualTo("sess-1"));
            Assert.That(working.UpdatedAt, Is.EqualTo(task.CreatedAt.AddMinutes(1)));
            Assert.That(waiting!.Status, Is.EqualTo(WorkTaskStatus.WaitingApproval));
            Assert.That(waiting.WaitsForHuman, Is.True);
            Assert.That(waiting.Summary, Is.EqualTo("Bash : dotnet test"));
            Assert.That(waiting.Operator.Session, Is.EqualTo("sess-1"), "What a change does not name stays.");
            Assert.That(waiting.Title, Is.EqualTo(task.Title));
            Assert.That(waiting.CreatedAt, Is.EqualTo(task.CreatedAt));
            Assert.That(board.FindBySession(TaskOperator.ClaudeCode, "sess-1"), Is.EqualTo(waiting));
            Assert.That(board.FindBySession(TaskOperator.ClaudeCode, "sess-2"), Is.Null);
            Assert.That(changes, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task A_change_that_changes_nothing_is_not_a_change()
    {
        var task = await board.CreateAsync(Asked);
        time.Advance(TimeSpan.FromMinutes(1));

        var same = await board.ChangeAsync(task.Id, new TaskChange(WorkTaskStatus.Pending));

        Assert.Multiple(() =>
        {
            Assert.That(same, Is.EqualTo(task));
            Assert.That(changes, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task A_task_that_ended_no_longer_moves()
    {
        var task = await board.CreateAsync(Asked);
        var done = await board.ChangeAsync(task.Id, new TaskChange(WorkTaskStatus.Done, "Fait."));

        // A hook that arrives late.
        var late = await board.ChangeAsync(task.Id, new TaskChange(WorkTaskStatus.Working, "Encore ?"));

        Assert.Multiple(() =>
        {
            Assert.That(late, Is.EqualTo(done));
            Assert.That(board.Find(task.Id)!.Status, Is.EqualTo(WorkTaskStatus.Done));
            Assert.That(board.Find(task.Id)!.Summary, Is.EqualTo("Fait."));
        });
    }

    [Test]
    public async Task A_task_nobody_knows_is_not_created_by_a_change() =>
        Assert.That(await board.ChangeAsync("task-unknown", new TaskChange(WorkTaskStatus.Working)), Is.Null);

    [Test]
    public async Task Sub_tasks_have_one_level_only()
    {
        var task = await board.CreateAsync(Asked);
        var sub = await board.CreateAsync(Asked with { Title = "Explorer", ParentId = task.Id, Status = WorkTaskStatus.Working });
        var deeper = await board.CreateAsync(Asked with { Title = "Plus loin", ParentId = sub.Id });
        var orphan = await board.CreateAsync(Asked with { Title = "Sans parent", ParentId = "task-gone" });

        Assert.Multiple(() =>
        {
            Assert.That(sub.ParentId, Is.EqualTo(task.Id));
            Assert.That(deeper.ParentId, Is.EqualTo(task.Id), "The sub-task of a sub-task hangs on the task.");
            Assert.That(orphan.ParentId, Is.Null);
        });
    }

    [Test]
    public async Task A_long_summary_is_shortened_and_never_kept_whole()
    {
        var task = await board.CreateAsync(Asked);

        var changed = await board.ChangeAsync(task.Id, new TaskChange(Summary: new string('x', 5000)));

        Assert.Multiple(() =>
        {
            Assert.That(changed!.Summary, Has.Length.EqualTo(TaskBoard.MostSummary));
            Assert.That(changed.Summary, Does.EndWith("…"));
        });
    }

    [Test]
    public async Task The_tasks_are_found_again_after_a_restart_as_they_were_left()
    {
        var first = await board.CreateAsync(Asked);
        time.Advance(TimeSpan.FromMinutes(1));
        var second = await board.CreateAsync(Asked with { Title = "Autre", Source = TaskSource.Hook });
        time.Advance(TimeSpan.FromMinutes(1));
        await board.ChangeAsync(first.Id, new TaskChange(WorkTaskStatus.Idle, "Tour fini.", "sess-1"));
        board.Dispose();

        board = Board();
        await board.StartAsync(CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(board.Tasks.Select(task => (task.Id, task.Status, task.Summary, task.Source)), Is.EqualTo(new[]
            {
                (first.Id, WorkTaskStatus.Idle, "Tour fini.", TaskSource.Harness),
                (second.Id, WorkTaskStatus.Pending, (string?)null, TaskSource.Hook),
            }), "The most recently changed first.");
            Assert.That(board.Find(first.Id)!.Operator.Session, Is.EqualTo("sess-1"));
            var stored = await store.Collection<WorkTask>(TaskBoard.Collection).ReadPeriodAsync(DateTimeOffset.UnixEpoch, time.GetUtcNow().AddDays(1));
            Assert.That(stored, Has.Count.EqualTo(2), "One document per task: a change replaces it.");
        });
    }

    [Test]
    public async Task The_ended_tasks_of_before_a_date_are_removed_with_their_sub_tasks()
    {
        var old = await board.CreateAsync(Asked);
        var sub = await board.CreateAsync(Asked with { Title = "Sous-tâche", ParentId = old.Id });
        await board.ChangeAsync(old.Id, new TaskChange(WorkTaskStatus.Done));
        var running = await board.CreateAsync(Asked with { Title = "En cours" });
        time.Advance(TimeSpan.FromDays(8));
        var recent = await board.CreateAsync(Asked with { Title = "Récente" });
        await board.ChangeAsync(recent.Id, new TaskChange(WorkTaskStatus.Abandoned));

        var removed = await board.RemoveEndedAsync(time.GetUtcNow().AddDays(-7));

        Assert.Multiple(async () =>
        {
            Assert.That(removed, Is.EqualTo(2));
            Assert.That(board.Find(old.Id), Is.Null);
            Assert.That(board.Find(sub.Id), Is.Null);
            Assert.That(board.Tasks.Select(task => task.Id), Is.EquivalentTo(new[] { running.Id, recent.Id }), "A task in progress is never removed, however old.");
            var stored = await store.Collection<WorkTask>(TaskBoard.Collection).ReadPeriodAsync(DateTimeOffset.UnixEpoch, time.GetUtcNow().AddDays(1));
            Assert.That(stored.Select(document => document.Content.Id), Is.EquivalentTo(new[] { running.Id, recent.Id }));
        });
    }

    private TaskBoard Board()
    {
        var created = new TaskBoard(store, time, NullLogger<TaskBoard>.Instance);
        created.Changed += () => changes++;
        return created;
    }
}
