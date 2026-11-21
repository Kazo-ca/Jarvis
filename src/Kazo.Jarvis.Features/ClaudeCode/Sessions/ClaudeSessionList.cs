using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.ClaudeCode.Sessions;

/// <summary>
/// The Claude Code sessions of the computer, grouped by project (US2.11, US2.14; ADR 0021, sections 6 and 7): the
/// registry is read at a regular interval, sessions started by hand and sessions of Claude Desktop included. A
/// session Jarvis follows as a task carries that task. Jarvis only looks: it never answers in the place of a
/// session it did not start.
/// </summary>
public sealed partial class ClaudeSessionList : IHostedService, IDisposable
{
    private readonly IClaudeSessionRegistry registry;
    private readonly ISettingsStore settings;
    private readonly ITaskBoard? board;
    private readonly TimeProvider time;
    private readonly ILogger<ClaudeSessionList> logger;
    private readonly IDisposable watch;
    private readonly SemaphoreSlim reading = new(1, 1);
    private readonly Lock gate = new();
    private ClaudeSessionsView view = ClaudeSessionsView.NotRead;
    private ITimer? timer;
    private bool started;
    private bool failed;

    /// <summary>Creates the list. Nothing is read before Jarvis starts.</summary>
    /// <param name="registry">The registry of the sessions.</param>
    /// <param name="settings">The settings, for <see cref="ClaudeSessionsOptions.Section"/>.</param>
    /// <param name="time">The clock of the readings.</param>
    /// <param name="logger">Logs a registry that cannot be read, never a session.</param>
    /// <param name="board">The tasks, when a board is there: a session followed as a task shows it.</param>
    public ClaudeSessionList(IClaudeSessionRegistry registry, ISettingsStore settings, TimeProvider time, ILogger<ClaudeSessionList> logger, ITaskBoard? board = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.registry = registry;
        this.settings = settings;
        this.time = time;
        this.logger = logger;
        this.board = board;
        watch = settings.Watch(ClaudeSessionsOptions.Section, _ => Schedule());
        if (board is not null)
        {
            board.Changed += OnBoardChanged;
        }
    }

    /// <summary>Raised when what the page shows changed; on any thread.</summary>
    public event Action? Changed;

    /// <summary>The sessions as last read.</summary>
    public ClaudeSessionsView View
    {
        get
        {
            lock (gate)
            {
                return view;
            }
        }
    }

    /// <summary>Groups <paramref name="sessions"/> by project: the projects with a session that waits first, then by last activity.</summary>
    /// <param name="sessions">The sessions read.</param>
    /// <param name="board">The tasks, to find the one of a session.</param>
    /// <returns>The groups.</returns>
    public static IReadOnlyList<ClaudeProjectGroup> Group(IReadOnlyList<ClaudeSession> sessions, ITaskBoard? board)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        return
        [
            .. sessions
                .Select(session => new ClaudeSessionRow(
                    session,
                    TaskKeys.Project(session.Remote, session.Folder),
                    TaskKeys.Topic(session.Branch),
                    board?.FindBySession(TaskOperator.ClaudeCode, session.SessionId)))
                .GroupBy(row => row.Project, StringComparer.Ordinal)
                .Select(group => new ClaudeProjectGroup(
                    group.Key,
                    TaskKeys.ProjectName(group.Key),
                    [.. group.OrderByDescending(row => row.Waits).ThenByDescending(row => row.Session.UpdatedAt ?? DateTimeOffset.MinValue).ThenBy(row => row.Session.SessionId, StringComparer.Ordinal)]))
                .OrderByDescending(group => group.Sessions.Any(row => row.Waits))
                .ThenByDescending(group => group.Sessions.Max(row => row.Session.UpdatedAt ?? DateTimeOffset.MinValue))
                .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>Reads the registry now, whatever the interval: the page asks when it opens.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>What was read.</returns>
    public async ValueTask<ClaudeSessionsView> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.Read(ClaudeSessionsOptions.Section).Enabled)
        {
            return Show(ClaudeSessionsView.Off);
        }

        await reading.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var listing = await registry.ReadAsync(cancellationToken).ConfigureAwait(false);
            failed = false;
            return Show(new ClaudeSessionsView(
                listing.Found ? ClaudeSessionsAvailability.Read : ClaudeSessionsAvailability.NoRegistry,
                Group(listing.Sessions, board),
                time.GetUtcNow()));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // An internal format of another program: a reading that fails is said once, and tried again.
            if (!failed)
            {
                failed = true;
                LogReadFailed(exception);
            }

            return Show(new ClaudeSessionsView(ClaudeSessionsAvailability.Unreadable, [], time.GetUtcNow()));
        }
        finally
        {
            reading.Release();
        }
    }

    /// <summary>Starts reading the registry at the set interval.</summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        started = true;
        Schedule();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        started = false;
        Schedule();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        started = false;
        Schedule();
        watch.Dispose();
        if (board is not null)
        {
            board.Changed -= OnBoardChanged;
        }

        reading.Dispose();
    }

    private void Schedule()
    {
        var options = settings.Read(ClaudeSessionsOptions.Section);
        ITimer? old;
        lock (gate)
        {
            old = timer;
            var period = TimeSpan.FromSeconds(options.PollSeconds);
            timer = started && options.Enabled ? time.CreateTimer(_ => _ = TickAsync(), null, TimeSpan.Zero, period) : null;
        }

        old?.Dispose();
        if (started && !options.Enabled)
        {
            Show(ClaudeSessionsView.Off);
        }
    }

    private async Task TickAsync()
    {
        try
        {
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ObjectDisposedException or OperationCanceledException)
        {
            // Jarvis is stopping.
        }
    }

    // A task that changed may be the one of a session shown: the same sessions, grouped again with their tasks.
    private void OnBoardChanged()
    {
        ClaudeSessionsView current;
        lock (gate)
        {
            current = view;
        }

        if (current.Availability == ClaudeSessionsAvailability.Read)
        {
            Show(current with { Projects = Group([.. current.Projects.SelectMany(project => project.Sessions).Select(row => row.Session)], board) });
        }
    }

    private ClaudeSessionsView Show(ClaudeSessionsView next)
    {
        bool changed;
        lock (gate)
        {
            // The time of the reading alone is not a change: the page is not redrawn every five seconds for nothing.
            changed = !view.SameContent(next);
            view = next;
        }

        if (changed)
        {
            Changed?.Invoke();
        }

        return next;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The registry of the Claude Code sessions could not be read.")]
    private partial void LogReadFailed(Exception exception);
}

/// <summary>Whether the sessions could be read.</summary>
public enum ClaudeSessionsAvailability
{
    /// <summary>Nothing was read yet.</summary>
    NotRead,

    /// <summary>The reading is turned off in the settings.</summary>
    Off,

    /// <summary>No registry: Claude Code never ran for this user.</summary>
    NoRegistry,

    /// <summary>The registry is there and could not be read.</summary>
    Unreadable,

    /// <summary>The registry was read.</summary>
    Read,
}

/// <summary>What the page shows.</summary>
/// <param name="Availability">Whether the sessions could be read.</param>
/// <param name="Projects">The sessions, grouped by project.</param>
/// <param name="ReadAt">When the registry was read.</param>
public sealed record ClaudeSessionsView(ClaudeSessionsAvailability Availability, IReadOnlyList<ClaudeProjectGroup> Projects, DateTimeOffset? ReadAt)
{
    /// <summary>Before the first reading.</summary>
    public static ClaudeSessionsView NotRead { get; } = new(ClaudeSessionsAvailability.NotRead, [], null);

    /// <summary>The reading is off.</summary>
    public static ClaudeSessionsView Off { get; } = new(ClaudeSessionsAvailability.Off, [], null);

    /// <summary>How many sessions are listed.</summary>
    public int Count => Projects.Sum(project => project.Sessions.Count);

    /// <summary>Whether <paramref name="other"/> shows the same thing, whatever the time of the reading.</summary>
    /// <param name="other">Another view.</param>
    /// <returns><see langword="true"/> when nothing to draw differs.</returns>
    public bool SameContent(ClaudeSessionsView other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Availability == other.Availability
            && Projects.Count == other.Projects.Count
            && Projects.Zip(other.Projects).All(pair => pair.First.Key == pair.Second.Key && pair.First.Sessions.SequenceEqual(pair.Second.Sessions));
    }
}

/// <summary>The sessions of one project.</summary>
/// <param name="Key">The project key (<see cref="TaskKeys.Project"/>).</param>
/// <param name="Name">The name shown.</param>
/// <param name="Sessions">Its sessions: those that wait first, then by last activity.</param>
public sealed record ClaudeProjectGroup(string Key, string Name, IReadOnlyList<ClaudeSessionRow> Sessions);

/// <summary>One session in the list.</summary>
/// <param name="Session">The session, as the registry tells it.</param>
/// <param name="Project">Its project key.</param>
/// <param name="Topic">Its topic key.</param>
/// <param name="Task">The task Jarvis follows for it, when there is one.</param>
public sealed record ClaudeSessionRow(ClaudeSession Session, string Project, string Topic, WorkTask? Task)
{
    /// <summary>Whether the session needs the user.</summary>
    public bool Waits => Session.State == ClaudeSessionState.Waiting || Task?.WaitsForHuman == true;

    /// <summary>Whether Jarvis started the session itself.</summary>
    public bool StartedByJarvis => Task?.Source == TaskSource.Harness;
}
