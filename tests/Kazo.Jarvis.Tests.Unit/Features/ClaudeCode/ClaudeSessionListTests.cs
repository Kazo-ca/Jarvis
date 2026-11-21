using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Adapters.Storage;
using Kazo.Jarvis.Features.ClaudeCode.Sessions;
using Kazo.Jarvis.Features.Tasks.Board;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tasks;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.ClaudeCode;

/// <summary>
/// The list of the Claude Code sessions (US2.11, US2.14; ADR 0021, sections 6 and 7): grouped by project and not
/// by raw path, the sessions that wait first, read at a regular interval, with the task Jarvis follows for a
/// session, and nothing read at all when the user turned it off.
/// </summary>
[TestFixture]
public sealed class ClaudeSessionListTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private TemporaryFolder folder = null!;
    private FakeTimeProvider time = null!;
    private JsonFileSettingsStore settings = null!;
    private SqliteDocumentStore store = null!;
    private TaskBoard board = null!;
    private FakeRegistry registry = null!;
    private ClaudeSessionList list = null!;
    private int changes;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        time = new FakeTimeProvider(Start);
        settings = new JsonFileSettingsStore(folder.Paths, [], time);
        store = new SqliteDocumentStore(folder.Paths, time);
        board = new TaskBoard(store, time, NullLogger<TaskBoard>.Instance);
        changes = 0;
        registry = new FakeRegistry();
        registry.Sessions.AddRange(
        [
            Session("bar-cli", @"C:\src\kazo-bar", ClaudeSessionState.Busy, 12, "https://github.com/Kazo-ca/bar.git", "feature/tray-menu"),
            Session("bar-desktop", @"C:\src\kazo-bar-review", ClaudeSessionState.Idle, 5, "git@github.com:Kazo-ca/Bar.git", "main") with { Entrypoint = ClaudeSession.DesktopEntrypoint },
            Session("notes", @"C:\Users\demo\Notes", ClaudeSessionState.Idle, 8),
        ]);
        list = new ClaudeSessionList(registry, settings, time, NullLogger<ClaudeSessionList>.Instance, board);
        list.Changed += () => Interlocked.Increment(ref changes);
    }

    [TearDown]
    public void TearDown()
    {
        list.Dispose();
        board.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        folder.Dispose();
    }

    [Test]
    public async Task The_sessions_are_grouped_by_project_whatever_their_folder()
    {
        var view = await list.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.Availability, Is.EqualTo(ClaudeSessionsAvailability.Read));
            Assert.That(view.Count, Is.EqualTo(3));
            Assert.That(view.Projects.Select(project => (project.Key, project.Name)), Is.EqualTo(new[]
            {
                ("github.com/kazo-ca/bar", "bar"),
                ("Notes", "Notes"),
            }), "Two clones of the same repository are one project; the most recently active project first.");
            Assert.That(view.Projects[0].Sessions.Select(row => (row.Session.SessionId, row.Topic)), Is.EqualTo(new[]
            {
                ("bar-cli", "branch:feature/tray-menu"),
                ("bar-desktop", "_inbox"),
            }));
            Assert.That(view.Projects[0].Sessions[1].Session.FromDesktop, Is.True);
            Assert.That(view.Projects.SelectMany(project => project.Sessions).Select(row => row.Task), Is.All.Null);
            Assert.That(list.View, Is.SameAs(view));
        });
    }

    [Test]
    public async Task A_session_that_waits_comes_first_with_its_project()
    {
        registry.Sessions[2] = registry.Sessions[2] with { State = ClaudeSessionState.Waiting, WaitingFor = "input needed", UpdatedAt = Start.AddMinutes(1) };

        var view = await list.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.Projects.Select(project => project.Name), Is.EqualTo(new[] { "Notes", "bar" }));
            Assert.That(view.Projects[0].Sessions.Single().Waits, Is.True);
        });
    }

    [Test]
    public async Task The_task_jarvis_follows_for_a_session_is_shown_with_it()
    {
        var task = await board.CreateAsync(new NewWorkTask("Corriger le menu", "github.com/kazo-ca/bar", "branch:feature/tray-menu", new TaskOperator(TaskOperator.ClaudeCode, "bar-cli"), TaskSource.Harness));
        await list.RefreshAsync();
        changes = 0;

        // The task moves between two readings of the registry: the list follows the board.
        await board.ChangeAsync(task.Id, new TaskChange(WorkTaskStatus.WaitingApproval));

        var row = list.View.Projects[0].Sessions[0];
        Assert.Multiple(() =>
        {
            Assert.That(row.Session.SessionId, Is.EqualTo("bar-cli"));
            Assert.That(row.Task!.Status, Is.EqualTo(WorkTaskStatus.WaitingApproval));
            Assert.That(row.StartedByJarvis, Is.True);
            Assert.That(row.Waits, Is.True, "The registry says busy; the task says it waits for the human.");
            Assert.That(registry.Reads, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task The_registry_is_read_at_the_set_interval_and_the_page_is_told_only_of_a_change()
    {
        await list.StartAsync(CancellationToken.None);
        await ReadsAsync(1);
        await ChangesAsync(1);

        time.Advance(TimeSpan.FromSeconds(5));
        await ReadsAsync(2);
        time.Advance(TimeSpan.FromSeconds(4));
        Assert.That(registry.Reads, Is.EqualTo(2), "Five seconds by default.");
        Assert.That(changes, Is.EqualTo(1), "The same sessions: nothing to redraw.");

        registry.Sessions[0] = registry.Sessions[0] with { State = ClaudeSessionState.Idle };
        time.Advance(TimeSpan.FromSeconds(1));
        await ReadsAsync(3);
        await ChangesAsync(2);
        Assert.That(list.View.Projects[0].Sessions[0].Session.State, Is.EqualTo(ClaudeSessionState.Idle));
    }

    [Test]
    public async Task Turned_off_nothing_is_read_at_all()
    {
        await settings.WriteAsync(ClaudeSessionsOptions.Section, new ClaudeSessionsOptions { Enabled = false });
        await list.StartAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromMinutes(1));

        var view = await list.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.Availability, Is.EqualTo(ClaudeSessionsAvailability.Off));
            Assert.That(view.Projects, Is.Empty);
            Assert.That(registry.Reads, Is.Zero, "Jarvis does not look at the Claude Code folder of the user.");
        });

        await settings.WriteAsync(ClaudeSessionsOptions.Section, new ClaudeSessionsOptions());
        await ReadsAsync(1);
    }

    [Test]
    public async Task No_registry_and_a_registry_that_fails_are_said_apart()
    {
        registry.Found = false;
        var none = await list.RefreshAsync();
        registry.Fail = true;
        var failed = await list.RefreshAsync();
        registry.Fail = false;
        registry.Found = true;
        var back = await list.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(none.Availability, Is.EqualTo(ClaudeSessionsAvailability.NoRegistry));
            Assert.That(failed.Availability, Is.EqualTo(ClaudeSessionsAvailability.Unreadable), "Never a fault of the assistant: tried again at the next reading.");
            Assert.That(back.Availability, Is.EqualTo(ClaudeSessionsAvailability.Read));
            Assert.That(back.Count, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task The_interval_has_bounds()
    {
        var low = await settings.WriteAsync(ClaudeSessionsOptions.Section, new ClaudeSessionsOptions { PollSeconds = 1 });
        var high = await settings.WriteAsync(ClaudeSessionsOptions.Section, new ClaudeSessionsOptions { PollSeconds = 61 });

        Assert.Multiple(() =>
        {
            Assert.That(low.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
            Assert.That(high.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
            Assert.That(settings.Read(ClaudeSessionsOptions.Section), Is.EqualTo(new ClaudeSessionsOptions()));
            Assert.That(new ClaudeSessionsOptions().Enabled, Is.True);
        });
    }

    private static ClaudeSession Session(string id, string folder, ClaudeSessionState state, int minutes, string? remote = null, string? branch = null) =>
        new(id, 1, folder, id, state, null, Start.AddMinutes(-60 + minutes), "cli", remote, branch);

    private async Task ReadsAsync(int expected)
    {
        for (var tries = 0; registry.Reads < expected && tries < 200; tries++)
        {
            await Task.Delay(10);
        }

        Assert.That(registry.Reads, Is.EqualTo(expected));
    }

    private async Task ChangesAsync(int expected)
    {
        for (var tries = 0; Volatile.Read(ref changes) < expected && tries < 200; tries++)
        {
            await Task.Delay(10);
        }

        Assert.That(changes, Is.EqualTo(expected));
    }

    private sealed class FakeRegistry : IClaudeSessionRegistry
    {
        private int reads;

        public List<ClaudeSession> Sessions { get; } = [];

        public bool Found { get; set; } = true;

        public bool Fail { get; set; }

        public int Reads => Volatile.Read(ref reads);

        public ValueTask<ClaudeSessionListing> ReadAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref reads);
            if (Fail)
            {
                throw new IOException("The format changed.");
            }

            return ValueTask.FromResult(Found ? new ClaudeSessionListing(true, [.. Sessions]) : ClaudeSessionListing.None);
        }
    }
}
