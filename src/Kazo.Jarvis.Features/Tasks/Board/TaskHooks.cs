using System.Text.Json;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.LocalServer;
using Kazo.Jarvis.Kernel.Tasks;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Tasks.Board;

/// <summary>Which hook of a Claude Code session called (ADR 0013, section 7; ADR 0021, section 5).</summary>
public enum TaskHookKind
{
    /// <summary><c>SessionStart</c>, route <c>/hook/task.context</c>: links the session to its task.</summary>
    Context,

    /// <summary><c>UserPromptSubmit</c>, route <c>/hook/task.start</c>: the task works.</summary>
    Start,

    /// <summary><c>PermissionRequest</c>, route <c>/hook/approval.requested</c>: the task waits for an approval.</summary>
    Approval,

    /// <summary><c>Stop</c>, route <c>/hook/task.idle</c>: the task is idle.</summary>
    Idle,

    /// <summary><c>SubagentStop</c>, route <c>/hook/subtask.idle</c>: a sub-task is done.</summary>
    SubtaskIdle,
}

/// <summary>
/// What the hooks of a Claude Code session do to the board (ADR 0021, sections 5 and 6). A hook only moves a
/// task: it decides nothing, and its answer is always empty, so that Claude Code goes on as if nobody listened.
/// The body is a piece of data: a few named fields are read from it, cut to length, and never executed nor
/// given to a model here.
/// <para>
/// A session started by Jarvis names its task by the header <see cref="LocalRequest.TaskHeader"/>; the task
/// exists before the session. A session started by hand has no task: its first hook creates one, of source
/// <see cref="TaskSource.Hook"/>, found again afterwards by the session identifier.
/// </para>
/// </summary>
public sealed partial class TaskHooks : IDisposable
{
    /// <summary>The text by which Claude Code starts a prompt that comes from a background task, not from a human.</summary>
    public const string NotificationStart = "<task-notification>";

    private const int MostIdentifier = 200;
    private const int MostTitleFromPrompt = 80;

    private readonly ITaskBoard board;
    private readonly IClaudeSessionRegistry? registry;
    private readonly ILogger<TaskHooks>? logger;

    // One hook at a time: the two first hooks of a session started by hand must not create two tasks.
    private readonly SemaphoreSlim turn = new(1, 1);

    /// <summary>Creates the handler of the hooks.</summary>
    /// <param name="board">The board the hooks move.</param>
    /// <param name="registry">The registry of the sessions, to know the repository and the branch of a session started by hand.</param>
    /// <param name="logger">The log. The text of a prompt or of an answer is never written to it.</param>
    public TaskHooks(ITaskBoard board, IClaudeSessionRegistry? registry = null, ILogger<TaskHooks>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        this.board = board;
        this.registry = registry;
        this.logger = logger;
    }

    /// <summary>Handles one hook.</summary>
    /// <param name="kind">Which hook.</param>
    /// <param name="request">The request, its token already checked by the server.</param>
    /// <param name="cancellationToken">Cancels the handling.</param>
    /// <returns>An empty answer, a refusal of the body, or « not found » for a task that does not exist.</returns>
    public async ValueTask<LocalResponse> HandleAsync(TaskHookKind kind, LocalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (HookBody.Read(request.Body) is not { } body)
        {
            return LocalResponse.BadRequest;
        }

        await turn.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WorkTask task;

            // Only a session Jarvis started may name a task: the token of the user's hooks opens no task by its name.
            if (request.Caller == LocalCaller.JarvisSession && request.TaskId is { } named)
            {
                if (board.Find(named) is not { } found)
                {
                    return LocalResponse.NotFound;
                }

                task = found;
            }
            else if (body.Session is null)
            {
                return LocalResponse.BadRequest;
            }
            else if (board.FindBySession(TaskOperator.ClaudeCode, body.Session) is { Ended: false } known)
            {
                task = known;
            }
            else
            {
                task = await CreateAsync(kind, body, cancellationToken).ConfigureAwait(false);
            }

            await ApplyAsync(kind, task, body, cancellationToken).ConfigureAwait(false);
            if (logger is not null)
            {
                LogHook(logger, kind, task.Id);
            }

            return LocalResponse.Done;
        }
        finally
        {
            turn.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => turn.Dispose();

    private async ValueTask<WorkTask> CreateAsync(TaskHookKind kind, HookBody body, CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(body.Session!, cancellationToken).ConfigureAwait(false);
        var folder = session?.Folder ?? body.Folder;
        var project = TaskKeys.Project(session?.Remote, folder);
        var title = kind == TaskHookKind.Start && body.HumanPrompt is { } prompt
            ? FirstLine(prompt)
            : session?.Name is { Length: > 0 } name ? name : TaskKeys.ProjectName(project);
        return await board.CreateAsync(
            new NewWorkTask(
                title,
                project,
                TaskKeys.Topic(session?.Branch),
                new TaskOperator(TaskOperator.ClaudeCode, body.Session),
                TaskSource.Hook,
                folder,
                Status: WorkTaskStatus.Idle),
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ApplyAsync(TaskHookKind kind, WorkTask task, HookBody body, CancellationToken cancellationToken)
    {
        // The session is linked by whichever hook comes first: SessionStart may be late or missing.
        var session = body.Session is not null && task.Operator.Session != body.Session ? body.Session : null;
        switch (kind)
        {
            case TaskHookKind.Context:
                if (session is not null)
                {
                    await board.ChangeAsync(task.Id, new TaskChange(Session: session), cancellationToken).ConfigureAwait(false);
                }

                break;
            case TaskHookKind.Start:
                await board.ChangeAsync(task.Id, new TaskChange(WorkTaskStatus.Working, Session: session), cancellationToken).ConfigureAwait(false);
                break;
            case TaskHookKind.Approval:
                await board.ChangeAsync(task.Id, new TaskChange(WorkTaskStatus.WaitingApproval, Session: session), cancellationToken).ConfigureAwait(false);
                break;
            case TaskHookKind.Idle:
                await board.ChangeAsync(task.Id, new TaskChange(WorkTaskStatus.Idle, body.LastMessage, session), cancellationToken).ConfigureAwait(false);
                break;
            case TaskHookKind.SubtaskIdle:
                await EndSubtaskAsync(task, body, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private async ValueTask EndSubtaskAsync(WorkTask task, HookBody body, CancellationToken cancellationToken)
    {
        if (body.Agent is null)
        {
            return;
        }

        var parent = task.ParentId ?? task.Id;
        var subtask = board.Tasks.FirstOrDefault(other => other.ParentId == parent && other.Operator.Session == body.Agent);
        if (subtask is null)
        {
            // No hook tells when a sub-agent starts: its sub-task appears when it ends.
            subtask = await board.CreateAsync(
                new NewWorkTask(
                    body.AgentType ?? "subagent",
                    task.Project,
                    task.Topic,
                    new TaskOperator(TaskOperator.ClaudeCode, body.Agent),
                    task.Source,
                    task.Folder,
                    parent,
                    WorkTaskStatus.Working),
                cancellationToken).ConfigureAwait(false);
        }

        await board.ChangeAsync(subtask.Id, new TaskChange(WorkTaskStatus.Done, body.LastMessage), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ClaudeSession?> FindSessionAsync(string session, CancellationToken cancellationToken)
    {
        if (registry is null)
        {
            return null;
        }

        try
        {
            var listing = await registry.ReadAsync(cancellationToken).ConfigureAwait(false);
            return listing.Sessions.FirstOrDefault(known => known.SessionId == session);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Without the registry the task is filed by the folder the hook gives.
            return null;
        }
    }

    private static string FirstLine(string prompt)
    {
        var line = prompt.AsSpan().Trim();
        var cut = line.IndexOfAny('\r', '\n');
        if (cut >= 0)
        {
            line = line[..cut];
        }

        return line.Length <= MostTitleFromPrompt ? line.ToString() : string.Concat(line[..(MostTitleFromPrompt - 1)].TrimEnd(), "…");
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Hook {Kind} received for the task {Task}.")]
    private static partial void LogHook(ILogger logger, TaskHookKind kind, string task);

    /// <summary>The few fields read from the body of a hook.</summary>
    private sealed record HookBody(string? Session, string? Folder, string? Prompt, string? LastMessage, string? Agent, string? AgentType)
    {
        /// <summary>The prompt, when a human wrote it.</summary>
        public string? HumanPrompt =>
            Prompt is { } text && text.Trim() is { Length: > 0 } trimmed && !trimmed.StartsWith(NotificationStart, StringComparison.Ordinal) ? trimmed : null;

        public static HookBody? Read(string text)
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                return new HookBody(
                    Identifier(root, "session_id"),
                    Text(root, "cwd"),
                    Text(root, "prompt"),
                    Text(root, "last_assistant_message"),
                    Identifier(root, "agent_id"),
                    Identifier(root, "agent_type"));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? Text(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

        // An identifier that is too long or holds a control character is not one.
        private static string? Identifier(JsonElement root, string name) =>
            Text(root, name)?.Trim() is { Length: > 0 and <= MostIdentifier } text && !text.Any(char.IsControl) ? text : null;
    }
}
