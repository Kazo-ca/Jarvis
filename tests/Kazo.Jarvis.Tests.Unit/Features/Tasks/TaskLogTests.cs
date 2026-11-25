using Kazo.Jarvis.Adapters.Storage;
using Kazo.Jarvis.Features.Tasks.Board;
using Kazo.Jarvis.Features.Tasks.Journal;
using Kazo.Jarvis.Kernel.Tasks;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.Tasks;

/// <summary>
/// The log of the tasks (ADR 0026, section 5): its lines are kept in order and found again after a restart, a
/// line is short and on one line, what looks like a secret is not kept, a log has an end, and the log of a task
/// that left the board leaves with it.
/// </summary>
[TestFixture]
public sealed class TaskLogTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private TemporaryFolder folder = null!;
    private FakeTimeProvider time = null!;
    private SqliteDocumentStore store = null!;
    private TaskBoard board = null!;
    private TaskLog log = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));
        store = new SqliteDocumentStore(folder.Paths, time);
        board = new TaskBoard(store, time, NullLogger<TaskBoard>.Instance);
        log = new TaskLog(store, board);
    }

    [TearDown]
    public void TearDown()
    {
        log.Dispose();
        board.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        folder.Dispose();
    }

    [Test]
    public async Task The_lines_of_a_task_are_kept_in_order_told_and_found_again_after_a_restart()
    {
        var told = new List<string>();
        log.Changed += told.Add;

        await log.AppendAsync(Line("t-1", TaskLogKind.Started, "claude-sonnet-5-5"));
        time.Advance(TimeSpan.FromSeconds(1));
        await log.AppendAsync(Line("t-2", TaskLogKind.Tool, "Read a.md"));
        time.Advance(TimeSpan.FromSeconds(1));
        await log.AppendAsync(Line("t-1", TaskLogKind.File, "Write", "/src/bar/NOTES.md"));

        using var again = new TaskLog(store);
        var read = await again.ReadAsync("t-1");
        Assert.Multiple(async () =>
        {
            Assert.That(told, Is.EqualTo(new[] { "t-1", "t-2", "t-1" }));
            Assert.That(read.Select(line => (line.Kind, line.Text, line.Path)), Is.EqualTo(new[]
            {
                (TaskLogKind.Started, "claude-sonnet-5-5", (string?)null),
                (TaskLogKind.File, "Write", "/src/bar/NOTES.md"),
            }));
            Assert.That(await again.ReadAsync("t-3"), Is.Empty);
        });
    }

    [Test]
    public async Task A_line_is_put_on_one_line_cut_and_never_keeps_what_looks_like_a_secret()
    {
        await log.AppendAsync(Line("t-1", TaskLogKind.Said, "Fait.\r\n\tTout est vert.  "));
        await log.AppendAsync(Line("t-1", TaskLogKind.Said, new string('a', TaskLog.MostText + 40)));
        await log.AppendAsync(Line("t-1", TaskLogKind.Tool, "Bash echo ghp_abcdefghijklmnopqrstuvwxyz0123456789"));

        var read = await log.ReadAsync("t-1");
        Assert.Multiple(() =>
        {
            Assert.That(read[0].Text, Is.EqualTo("Fait. Tout est vert."));
            Assert.That((read[1].Text.Length, read[1].Text[^1]), Is.EqualTo((TaskLog.MostText, '…')));
            Assert.That(read[2].Text, Is.EqualTo(TaskLog.Masked));
        });
    }

    [Test]
    public async Task A_log_that_is_full_drops_the_next_lines()
    {
        for (var index = 0; index < TaskLog.MostEntries + 3; index++)
        {
            await log.AppendAsync(Line("t-1", TaskLogKind.Tool, "Read " + index));
        }

        await log.AppendAsync(Line("t-2", TaskLogKind.Tool, "Read"));

        Assert.Multiple(async () =>
        {
            Assert.That(await log.ReadAsync("t-1"), Has.Count.EqualTo(TaskLog.MostEntries));
            Assert.That(await log.ReadAsync("t-2"), Has.Count.EqualTo(1), "The log of another task is not full.");
        });
    }

    [Test]
    public async Task The_log_of_a_task_that_left_the_board_is_erased_with_it()
    {
        var gone = await board.CreateAsync(Asked("Corriger le menu"));
        await board.ChangeAsync(gone.Id, new TaskChange(WorkTaskStatus.Done, "Fait."));
        time.Advance(TimeSpan.FromDays(2));
        var kept = await board.CreateAsync(Asked("Ajouter un test"));
        await log.AppendAsync(Line(gone.Id, TaskLogKind.Tool, "Read a.md"));
        await log.AppendAsync(Line(kept.Id, TaskLogKind.Tool, "Read b.md"));

        await board.RemoveEndedAsync(time.GetUtcNow().AddDays(-1));

        using var limit = new CancellationTokenSource(Wait);
        while ((await new TaskLog(store).ReadAsync(gone.Id)).Count > 0)
        {
            await Task.Delay(10, limit.Token);
        }

        Assert.Multiple(async () =>
        {
            Assert.That(await log.ReadAsync(gone.Id), Is.Empty);
            Assert.That(await log.ReadAsync(kept.Id), Has.Count.EqualTo(1));
        });
    }

    private static NewWorkTask Asked(string title) =>
        new(title, "github.com/kazo-ca/bar", "branch:main", new TaskOperator(TaskOperator.ClaudeCode), TaskSource.Harness, "/src/bar");

    private TaskLogEntry Line(string task, TaskLogKind kind, string text, string? path = null) =>
        new(task, time.GetUtcNow(), kind, text, path);
}
