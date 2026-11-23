namespace Kazo.Jarvis.Kernel.Tasks;

/// <summary>
/// A task, in the shape and the vocabulary of Relais (ADR 0013, section 6): what the stages 2 and 3 show as
/// « tâche en cours » — a Claude Code session, a delegated task, a panel of the fan. Named <c>WorkTask</c> so
/// that it is never mistaken for a <see cref="System.Threading.Tasks.Task"/>.
/// </summary>
/// <param name="Id">The identifier, given to a session as <c>RELAIS_TASK_ID</c>.</param>
/// <param name="ParentId">The parent task: empty for a task, set for a sub-task. One level only.</param>
/// <param name="Project">The project key (<see cref="TaskKeys.Project"/>).</param>
/// <param name="Topic">The topic key (<see cref="TaskKeys.Topic"/>).</param>
/// <param name="Title">The title shown to the user.</param>
/// <param name="Summary">What the task says of itself: its last message, or the cause when it is blocked.</param>
/// <param name="Operator">Who does the work.</param>
/// <param name="Source">Where the task was learned from.</param>
/// <param name="Status">One of the nine statuses.</param>
/// <param name="CreatedAt">When the task was created.</param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="Folder">The folder the work happens in; an addition of Jarvis to the shape, for the list of the sessions (US2.11).</param>
public sealed record WorkTask(
    string Id,
    string? ParentId,
    string Project,
    string Topic,
    string Title,
    string? Summary,
    TaskOperator Operator,
    TaskSource Source,
    WorkTaskStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Folder = null)
{
    /// <summary>Whether the task waits for the human: amber in the fan (ADR 0013, section 6).</summary>
    public bool WaitsForHuman => Status is WorkTaskStatus.WaitingAnswer or WorkTaskStatus.WaitingApproval;

    /// <summary>Whether the task is over: <c>done</c> or <c>abandoned</c>.</summary>
    public bool Ended => Status is WorkTaskStatus.Done or WorkTaskStatus.Abandoned;
}

/// <summary>Who does the work of a task (ADR 0013, section 6).</summary>
/// <param name="Account">The account, for instance <c>claude-code</c>.</param>
/// <param name="Session">The session of that account, once known.</param>
public sealed record TaskOperator(string Account, string? Session = null)
{
    /// <summary>The account of every Claude Code session.</summary>
    public const string ClaudeCode = "claude-code";
}

/// <summary>What is known of a task when it is created.</summary>
/// <param name="Title">The title.</param>
/// <param name="Project">The project key.</param>
/// <param name="Topic">The topic key.</param>
/// <param name="Operator">Who does the work.</param>
/// <param name="Source">Where the task is learned from.</param>
/// <param name="Folder">The folder the work happens in.</param>
/// <param name="ParentId">The parent task, for a sub-task.</param>
/// <param name="Status">The first status; <c>pending</c> for a task Jarvis starts (ADR 0021, section 5).</param>
public sealed record NewWorkTask(
    string Title,
    string Project,
    string Topic,
    TaskOperator Operator,
    TaskSource Source,
    string? Folder = null,
    string? ParentId = null,
    WorkTaskStatus Status = WorkTaskStatus.Pending);

/// <summary>The nine statuses of the Relais specification, and no other (ADR 0013, section 6).</summary>
public enum WorkTaskStatus
{
    /// <summary><c>pending</c>: created, not started.</summary>
    Pending,

    /// <summary><c>working</c>.</summary>
    Working,

    /// <summary><c>paused</c>.</summary>
    Paused,

    /// <summary><c>idle</c>: its turn ended; it may get another one.</summary>
    Idle,

    /// <summary><c>waiting_answer</c>: it asked the human a question.</summary>
    WaitingAnswer,

    /// <summary><c>waiting_approval</c>: it waits for a permission.</summary>
    WaitingApproval,

    /// <summary><c>blocked</c>: it cannot go on; the summary says why.</summary>
    Blocked,

    /// <summary><c>done</c>.</summary>
    Done,

    /// <summary><c>abandoned</c>: stopped by the user.</summary>
    Abandoned,
}

/// <summary>Where a task was learned from (ADR 0013, section 6).</summary>
public enum TaskSource
{
    /// <summary><c>harness</c>: Jarvis started the session itself.</summary>
    Harness,

    /// <summary><c>hook</c>: a hook of a session started by hand told it.</summary>
    Hook,

    /// <summary><c>mcp</c>: an agent declared it through an MCP tool.</summary>
    Mcp,

    /// <summary><c>orchestrator</c>: an orchestrator created it (Relais, v2).</summary>
    Orchestrator,
}

/// <summary>The names of the statuses and of the sources as the Relais specification writes them.</summary>
public static class TaskWire
{
    /// <summary>The name of <paramref name="status"/> in the specification, for instance <c>waiting_approval</c>.</summary>
    /// <param name="status">A status.</param>
    /// <returns>Its name, in snake case.</returns>
    public static string ToWire(this WorkTaskStatus status) => status switch
    {
        WorkTaskStatus.Pending => "pending",
        WorkTaskStatus.Working => "working",
        WorkTaskStatus.Paused => "paused",
        WorkTaskStatus.Idle => "idle",
        WorkTaskStatus.WaitingAnswer => "waiting_answer",
        WorkTaskStatus.WaitingApproval => "waiting_approval",
        WorkTaskStatus.Blocked => "blocked",
        WorkTaskStatus.Done => "done",
        WorkTaskStatus.Abandoned => "abandoned",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    /// <summary>The name of <paramref name="source"/> in the specification.</summary>
    /// <param name="source">A source.</param>
    /// <returns>Its name, in lower case.</returns>
    public static string ToWire(this TaskSource source) => source switch
    {
        TaskSource.Harness => "harness",
        TaskSource.Hook => "hook",
        TaskSource.Mcp => "mcp",
        TaskSource.Orchestrator => "orchestrator",
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };
}
