using System.Globalization;
using System.Resources;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Features.ClaudeCode.Launch;

/// <summary>A session Jarvis started, for the page.</summary>
/// <param name="TaskId">Its task.</param>
/// <param name="Folder">Its folder.</param>
/// <param name="Name">The name of the folder, as said.</param>
/// <param name="StartedAt">When it started.</param>
public sealed record RunningSession(string TaskId, string Folder, string Name, DateTimeOffset StartedAt);

/// <summary>
/// The Claude Code sessions Jarvis starts (US2.9, US2.10; ADR 0021, sections 1, 4, 5 and 8). It chooses the folder
/// of a work among the allowed ones or asks which, creates the task before the launch, starts the session
/// read-only, and follows its stream until the process ends. Every write or command of the session arrives here
/// by the <c>approve</c> tool and goes to the single decision point; a session where a write or a command runs
/// without having asked is stopped.
/// </summary>
public sealed partial class ClaudeTaskRunner : IClaudeTasks, IClaudeApprovals, IHostedService, IAsyncDisposable
{
    /// <summary>The longest title of a task, cut from the request.</summary>
    public const int MostTitle = 80;

    // A question « sur quel dossier ? » is answered in the next moments, or forgotten.
    private static readonly TimeSpan QuestionLife = TimeSpan.FromMinutes(2);

    private static readonly ResourceManager Texts = new(typeof(ClaudeLaunchTexts));

    private readonly ITaskBoard board;
    private readonly IPermissionDecision decisions;
    private readonly ISettingsStore settings;
    private readonly IMessageBus bus;
    private readonly TimeProvider time;
    private readonly IClaudeCode? claude;
    private readonly IAllowedFolders? folders;
    private readonly ISpendCap? spend;
    private readonly ILogger<ClaudeTaskRunner> logger;
    private readonly Lock gate = new();
    private readonly Dictionary<string, Session> sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource> opening = new(StringComparer.Ordinal);
    private int starting;
    private (ClaudeTaskRequest Request, DateTimeOffset At)? question;
    private bool closing;

    /// <summary>Creates the runner.</summary>
    /// <param name="board">The tasks.</param>
    /// <param name="decisions">The decision point of the permissions.</param>
    /// <param name="settings">The settings, for <see cref="ClaudeLaunchOptions.Section"/>.</param>
    /// <param name="bus">Where the end of a session is published.</param>
    /// <param name="time">The clock.</param>
    /// <param name="claude">Claude Code on this computer, when the host can start it.</param>
    /// <param name="folders">The allowed folders, when the host has files.</param>
    /// <param name="spend">The spending cap, when a spending slice is there.</param>
    /// <param name="logger">The log. Never what a session wrote.</param>
    public ClaudeTaskRunner(
        ITaskBoard board,
        IPermissionDecision decisions,
        ISettingsStore settings,
        IMessageBus bus,
        TimeProvider time,
        IClaudeCode? claude = null,
        IAllowedFolders? folders = null,
        ISpendCap? spend = null,
        ILogger<ClaudeTaskRunner>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(time);
        this.board = board;
        this.decisions = decisions;
        this.settings = settings;
        this.bus = bus;
        this.time = time;
        this.claude = claude;
        this.folders = folders;
        this.spend = spend;
        this.logger = logger ?? NullLogger<ClaudeTaskRunner>.Instance;
    }

    /// <summary>Raised after a session started or ended; on any thread.</summary>
    public event Action? Changed;

    /// <summary>Whether this host can start Claude Code at all.</summary>
    public bool Available => claude is not null;

    /// <summary>The allowed folders a session can be started on.</summary>
    public IReadOnlyList<string> Folders => folders?.Roots ?? [];

    /// <summary>The sessions running now, the oldest first.</summary>
    public IReadOnlyList<RunningSession> Sessions
    {
        get
        {
            lock (gate)
            {
                return [.. sessions.Values.OrderBy(session => session.StartedAt).Select(session => new RunningSession(session.TaskId, session.Folder, session.Name, session.StartedAt))];
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Running
    {
        get
        {
            lock (gate)
            {
                return [.. sessions.Keys];
            }
        }
    }

    /// <summary>A text of <c>ClaudeLaunchTexts.resx</c> in <paramref name="language"/>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="language">The language.</param>
    /// <param name="arguments">The values of its placeholders.</param>
    /// <returns>The text.</returns>
    public static string Text(string key, AssistantLanguage language, params object[] arguments)
    {
        ArgumentNullException.ThrowIfNull(language);
        var text = Texts.GetString(key, language.Culture) ?? key;
        return arguments.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, arguments);
    }

    /// <summary>Looks for Claude Code, for the page.</summary>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>What was found; <see langword="null"/> on a host that cannot start it.</returns>
    public async ValueTask<ClaudeCodeInstall?> FindAsync(CancellationToken cancellationToken = default) =>
        claude is null ? null : await claude.FindAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public ClaudeTaskRequest? Resume(string request)
    {
        (ClaudeTaskRequest Request, DateTimeOffset At)? asked;
        lock (gate)
        {
            // One answer only: whatever is said next, the question is over.
            (asked, question) = (question, null);
        }

        if (asked is not { } waiting || string.IsNullOrWhiteSpace(request) || time.GetUtcNow() - waiting.At > QuestionLife)
        {
            return null;
        }

        return FolderChoice.Named(request, Folders) is [var folder] ? waiting.Request with { Folder = folder } : null;
    }

    /// <inheritdoc />
    public async ValueTask<ClaudeTaskOpening> StartAsync(ClaudeTaskRequest request, Func<string, ValueTask> announce, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(announce);
        var language = request.Language;
        if (claude is null)
        {
            return Refused("NoClaudeCode", language);
        }

        // 1. The folder: one the request names, the only one there is, or a question.
        var roots = Folders;
        if (roots.Count == 0)
        {
            return Refused("NoFolder", language);
        }

        string folder;
        if (request.Folder is { } chosen)
        {
            if (!roots.Contains(chosen, StringComparer.OrdinalIgnoreCase))
            {
                return Refused("NoFolder", language);
            }

            folder = chosen;
        }
        else if (FolderChoice.Named(request.Request, roots) is [var named])
        {
            folder = named;
        }
        else if (roots.Count == 1)
        {
            folder = roots[0];
        }
        else
        {
            lock (gate)
            {
                question = (request, time.GetUtcNow());
            }

            return new ClaudeTaskOpening(false, Text("WhichFolder", language, Names(roots, language)));
        }

        var name = FolderChoice.NameOf(folder);
        var options = settings.Read(ClaudeLaunchOptions.Section);

        // 2. A place among the sessions, kept while this one starts.
        lock (gate)
        {
            if (closing || sessions.Count + starting >= options.MostSessions)
            {
                return Refused("TooMany", language, sessions.Count + starting);
            }

            starting++;
        }

        WorkTask? task = null;
        try
        {
            var install = await claude.FindAsync(cancellationToken).ConfigureAwait(false);
            if (!install.Found)
            {
                return Refused("NotInstalled", language);
            }

            // 3. Never more than what is left of the monthly cap when Claude Code counts (ADR 0021, section 1).
            var budget = spend?.SessionBudget(options.SessionBudgetUsd) ?? options.SessionBudgetUsd;
            if (budget <= 0)
            {
                return Refused("CapReached", language);
            }

            // 4. The folder is said first; an interruption during that sentence cancels (section 8).
            await announce(Text("Launching", language, name)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // 5. The task exists before the launch (ADR 0013, section 7).
            task = await board.CreateAsync(
                new NewWorkTask(Title(request.Request), TaskKeys.Project(null, folder), TaskKeys.Inbox, new TaskOperator(TaskOperator.ClaudeCode), TaskSource.Harness, folder),
                CancellationToken.None).ConfigureAwait(false);
            var context = request.Context is null ? null : await request.Context(cancellationToken).ConfigureAwait(false);
            var launch = new ClaudeLaunch(task.Id, folder, FirstMessage(request.Request, context, language))
            {
                Trusted = options.IsTrusted(folder),
                BudgetUsd = budget,
                Model = options.Model,
            };
            // A session may ask its first permission before the launch returns: the question waits for the session
            // to be known rather than being refused as a stranger's.
            var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
                opening[task.Id] = opened;
            }

            Session session;
            try
            {
                var run = await claude.StartAsync(launch, cancellationToken).ConfigureAwait(false);
                session = new Session(task.Id, folder, name, language, run, time.GetUtcNow());
                lock (gate)
                {
                    sessions[task.Id] = session;
                }
            }
            finally
            {
                lock (gate)
                {
                    opening.Remove(task.Id);
                }

                opened.TrySetResult();
            }

            LogStarted(task.Id, launch.Trusted);
            session.Following = Task.Run(() => FollowAsync(session));
            Changed?.Invoke();
            return new ClaudeTaskOpening(true, Text("Started", language, name), task.Id);
        }
        catch (ClaudeCodeException exception)
        {
            LogStartFailed(exception, task?.Id);
            if (task is not null)
            {
                await board.ChangeAsync(task.Id, new TaskChange(WorkTaskStatus.Blocked, Text("StartFailed", language)), CancellationToken.None).ConfigureAwait(false);
            }

            return Refused("StartFailed", language);
        }
        catch (OperationCanceledException) when (task is not null)
        {
            // Interrupted between the creation of the task and the start: nothing runs.
            await board.ChangeAsync(task.Id, new TaskChange(WorkTaskStatus.Abandoned, Text("Abandoned", language)), CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            lock (gate)
            {
                starting--;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> StopAsync(string taskId)
    {
        Session? session;
        lock (gate)
        {
            sessions.TryGetValue(taskId, out session);
        }

        if (session is null)
        {
            return false;
        }

        session.End(ClaudeTaskEnd.Abandoned, Text("Abandoned", session.Language));
        await session.Run.StopAsync().ConfigureAwait(false);
        return true;
    }

    // The stream of a session and its permission calls arrive on two paths: one at a time, so that a line of the
    // stream never puts back to work a task that waits for an answer (ADR 0021, section 5).
    private async Task StatusAsync(Session session, int waits, string? sessionId = null)
    {
        await session.Status.WaitAsync().ConfigureAwait(false);
        try
        {
            session.Waiting = Math.Max(0, session.Waiting + waits);
            var status = session.Waiting > 0 ? WorkTaskStatus.WaitingApproval : WorkTaskStatus.Working;
            await board.ChangeAsync(session.TaskId, new TaskChange(status, Session: sessionId), CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            session.Status.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<ClaudeApproval> AskAsync(ClaudeApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Session? session = null;
        if (request.TaskId is { } id)
        {
            Task? opened = null;
            lock (gate)
            {
                if (!sessions.TryGetValue(id, out session) && opening.TryGetValue(id, out var pending))
                {
                    opened = pending.Task;
                }
            }

            if (opened is not null)
            {
                await opened.WaitAsync(cancellationToken).ConfigureAwait(false);
                lock (gate)
                {
                    sessions.TryGetValue(id, out session);
                }
            }
        }

        if (session is null)
        {
            // Not a session Jarvis follows: nothing is decided for it (ADR 0021, section 6).
            LogUnknownSession(request.ToolName);
            return ClaudeApproval.Deny("Jarvis does not know this session: nothing is allowed.");
        }

        session.Asked(request.ToolUseId, request.ToolName);
        var folderList = folders;
        var verdict = folderList is null
            ? new SessionVerdict(ActionClass.Forbidden, SessionRequestKind.Other)
            : SessionRules.Classify(request.ToolName, request.Input, session.Folder, folderList);
        var asks = verdict.Class is ActionClass.Sensitive or ActionClass.Critical;
        if (asks)
        {
            await StatusAsync(session, waits: 1).ConfigureAwait(false);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.Stopping.Token);
        PermissionDecision decision;
        try
        {
            decision = await decisions.DecideAsync(
                new ActionRequest(new ActionRequester(RequesterKind.Session, session.Name), request.ToolName, request.Input)
                {
                    Session = new SessionAction(verdict.Class, Summary(verdict, request.ToolName, session)),
                },
                linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Claude Code gave the call up, or the session is being stopped: no answer is an agreement.
            decision = PermissionDecision.Deny(DenialCause.Interrupted, verdict.Class);
        }

        if (asks && !session.Ended)
        {
            // The decision puts the task back to work (ADR 0021, section 5).
            await StatusAsync(session, waits: -1).ConfigureAwait(false);
        }

        LogDecided(session.TaskId, request.ToolName, decision.Allowed, decision.Cause);
        return decision.Allowed ? ClaudeApproval.Allow : ClaudeApproval.Deny(Denial(decision.Cause));
    }

    /// <summary>
    /// A task Jarvis started and that never ended has no session after a restart: it is blocked, with the cause
    /// (ADR 0021, section 5).
    /// </summary>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <returns>A task that ends once the tasks are put right.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var task in board.Tasks.Where(task => task is { Source: TaskSource.Harness, Ended: false, Status: not WorkTaskStatus.Blocked, Operator.Account: TaskOperator.ClaudeCode }))
            {
                await board.ChangeAsync(task.Id, new TaskChange(WorkTaskStatus.Blocked, Text("Blocked_Closed", AssistantLanguage.English)), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogBoardFailed(exception);
        }
    }

    /// <summary>Stops every session: none goes on writing once Jarvis no longer answers its requests.</summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A task that ends once the sessions are gone.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        List<Session> running;
        lock (gate)
        {
            closing = true;
            running = [.. sessions.Values];
        }

        foreach (var session in running)
        {
            session.End(ClaudeTaskEnd.Blocked, Text("Blocked_Closed", session.Language));
            try
            {
                await session.Run.StopAsync().ConfigureAwait(false);
                if (session.Following is { } following)
                {
                    await following.ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                LogFollowFailed(exception, session.TaskId);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None).ConfigureAwait(false);

    /// <summary>The first message of a session (ADR 0021, section 8): the summary as a context, then the request word for word.</summary>
    internal static string FirstMessage(string request, string? context, AssistantLanguage language) =>
        string.IsNullOrWhiteSpace(context)
            ? request
            : $"{Text("ContextLabel", language)}\n{context.Trim()}\n\n{Text("RequestLabel", language)}\n{request}";

    private static string Title(string request)
    {
        var line = string.Join(' ', request.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= MostTitle ? line : line[..(MostTitle - 1)].TrimEnd() + "…";
    }

    private static string Names(IReadOnlyList<string> roots, AssistantLanguage language)
    {
        var names = roots.Select(FolderChoice.NameOf).ToList();
        return names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + Text("Or", language) + names[^1];
    }

    private static ClaudeTaskOpening Refused(string key, AssistantLanguage language, params object[] arguments) =>
        new(false, Text(key, language, arguments));

    /// <summary>The sentence of a confirmation request: the folder, the tool and the path or the whole command (ADR 0021, section 4).</summary>
    private static string? Summary(SessionVerdict verdict, string tool, Session session) => verdict.Kind switch
    {
        SessionRequestKind.Write when verdict.Path is { } path => Text("Summary_Write", session.Language, path, session.Name),
        SessionRequestKind.Command when verdict.Command is { } command => Text("Summary_Command", session.Language, command, session.Name),
        _ => Text("Summary_Other", session.Language, tool, session.Name),
    };

    // Told to the session, a model: in English, and never more than the cause.
    private static string Denial(DenialCause? cause) => cause switch
    {
        DenialCause.Forbidden => "Jarvis refuses: a session works in its own folder only, and never on the key or lock files of Claude Code.",
        DenialCause.Refused => "The user refused.",
        DenialCause.Interrupted => "The request was interrupted before the user answered.",
        DenialCause.ConfirmationUnavailable => "The user cannot be asked now: nothing is allowed.",
        _ => "Jarvis could not decide: nothing is allowed.",
    };

    private async Task FollowAsync(Session session)
    {
        string? last = null;
        decimal? cost = null;
        var result = false;
        var failed = false;
        try
        {
            await foreach (var happened in session.Run.ReadAsync(CancellationToken.None).ConfigureAwait(false))
            {
                switch (happened)
                {
                    case ClaudeStarted started:
                        await StatusAsync(session, waits: 0, started.SessionId).ConfigureAwait(false);
                        break;

                    case ClaudeToolUse use when SessionRules.WritesOrRuns(use.Name):
                        session.Uses[use.ToolUseId] = use.Name;
                        break;

                    case ClaudeToolResult { IsError: false } done when session.Uses.Remove(done.ToolUseId, out var tool) && !session.WasAsked(done.ToolUseId, tool):
                        // A write or a command ran and « approve » never saw it: the permissions do not arrive, so
                        // the session is stopped rather than left writing (ADR 0021, section 1).
                        LogUnasked(session.TaskId, tool);
                        session.End(ClaudeTaskEnd.Blocked, Text("Blocked_Watchdog", session.Language, tool));
                        await session.Run.StopAsync().ConfigureAwait(false);
                        break;

                    case ClaudeTurnEnded turn:
                        (last, cost, result, failed) = (turn.Text ?? last, turn.CostUsd ?? cost, true, turn.IsError);
                        if (!session.Ended)
                        {
                            await board.ChangeAsync(session.TaskId, new TaskChange(WorkTaskStatus.Idle, Cut(turn.Text)), CancellationToken.None).ConfigureAwait(false);
                        }

                        break;

                    case ClaudeExited exited:
                        LogExited(session.TaskId, exited.ExitCode);
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            LogFollowFailed(exception, session.TaskId);
        }

        // A task is done only at a result of the stream, with the process gone: background work is over then
        // (ADR 0021, section 5). Without a result it is blocked, with the cause.
        var (end, summary) = session.Outcome
            ?? (result && !failed ? (ClaudeTaskEnd.Done, last)
                : result ? (ClaudeTaskEnd.Blocked, last ?? Text("Blocked_Error", session.Language))
                : (ClaudeTaskEnd.Blocked, Text("Blocked_NoResult", session.Language)));
        session.End(end, summary);
        session.Stopping.Cancel();
        lock (gate)
        {
            sessions.Remove(session.TaskId);
        }

        try
        {
            var status = end switch
            {
                ClaudeTaskEnd.Done => WorkTaskStatus.Done,
                ClaudeTaskEnd.Abandoned => WorkTaskStatus.Abandoned,
                _ => WorkTaskStatus.Blocked,
            };
            await board.ChangeAsync(session.TaskId, new TaskChange(status, Cut(summary)), CancellationToken.None).ConfigureAwait(false);
            await bus.PublishAsync(new ClaudeTaskEnded(session.TaskId, session.Folder, end, Cut(summary), cost, session.Language, time.GetUtcNow()), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogFollowFailed(exception, session.TaskId);
        }

        await session.Run.DisposeAsync().ConfigureAwait(false);
        Changed?.Invoke();
    }

    private static string? Cut(string? text) =>
        text is null || text.Length <= 2000 ? text : text[..1999] + "…";

    [LoggerMessage(Level = LogLevel.Information, Message = "Claude Code session started for task {Task} (trusted folder: {Trusted}).")]
    private partial void LogStarted(string task, bool trusted);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Claude Code did not start for task {Task}.")]
    private partial void LogStartFailed(Exception exception, string? task);

    [LoggerMessage(Level = LogLevel.Information, Message = "Claude Code session of task {Task} exited with code {Code}.")]
    private partial void LogExited(string task, int code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Task {Task}: the tool {Tool} ran without a permission request; the session is stopped.")]
    private partial void LogUnasked(string task, string tool);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A permission request for {Tool} named no session Jarvis follows: denied.")]
    private partial void LogUnknownSession(string tool);

    [LoggerMessage(Level = LogLevel.Information, Message = "Task {Task}: request for {Tool} decided (allowed: {Allowed}, cause: {Cause}).")]
    private partial void LogDecided(string task, string tool, bool allowed, DenialCause? cause);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Following the session of task {Task} failed.")]
    private partial void LogFollowFailed(Exception exception, string task);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The tasks left by a previous run could not be put right.")]
    private partial void LogBoardFailed(Exception exception);

    /// <summary>One running session.</summary>
    private sealed class Session(string taskId, string folder, string name, AssistantLanguage language, IClaudeRun run, DateTimeOffset startedAt)
    {
        private readonly Lock gate = new();
        private readonly HashSet<string> askedIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> askedTools = new(StringComparer.Ordinal);

        public string TaskId { get; } = taskId;

        public string Folder { get; } = folder;

        public string Name { get; } = name;

        public AssistantLanguage Language { get; } = language;

        public IClaudeRun Run { get; } = run;

        public DateTimeOffset StartedAt { get; } = startedAt;

        /// <summary>Cancelled when the session ends: a confirmation still open for it closes.</summary>
        public CancellationTokenSource Stopping { get; } = new();

        /// <summary>The write and command tools seen in the stream, not yet run. Touched by the following loop only.</summary>
        public Dictionary<string, string> Uses { get; } = new(StringComparer.Ordinal);

        public Task? Following { get; set; }

        /// <summary>One change of status at a time.</summary>
        public SemaphoreSlim Status { get; } = new(1, 1);

        /// <summary>The permission requests waiting for a human answer. Touched under <see cref="Status"/> only.</summary>
        public int Waiting { get; set; }

        /// <summary>How the session ends, once known: the first cause wins.</summary>
        public (ClaudeTaskEnd End, string? Summary)? Outcome { get; private set; }

        public bool Ended => Outcome is not null;

        public void End(ClaudeTaskEnd end, string? summary)
        {
            lock (gate)
            {
                Outcome ??= (end, summary);
            }
        }

        public void Asked(string? toolUseId, string tool)
        {
            lock (gate)
            {
                if (toolUseId is not null)
                {
                    askedIds.Add(toolUseId);
                }
                else
                {
                    askedTools[tool] = askedTools.GetValueOrDefault(tool) + 1;
                }
            }
        }

        public bool WasAsked(string toolUseId, string tool)
        {
            lock (gate)
            {
                if (askedIds.Remove(toolUseId))
                {
                    return true;
                }

                // A request that named no use counts for one use of its tool.
                if (askedTools.GetValueOrDefault(tool) is > 0 and var left)
                {
                    askedTools[tool] = left - 1;
                    return true;
                }

                return false;
            }
        }
    }
}

/// <summary>Marker of the resources <c>ClaudeLaunchTexts.resx</c> and <c>ClaudeLaunchTexts.fr.resx</c>.</summary>
public sealed class ClaudeLaunchTexts
{
    private ClaudeLaunchTexts()
    {
    }
}
