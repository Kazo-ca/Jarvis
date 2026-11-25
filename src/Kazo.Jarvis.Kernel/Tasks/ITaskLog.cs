namespace Kazo.Jarvis.Kernel.Tasks;

/// <summary>What a line of the log of a task tells.</summary>
public enum TaskLogKind
{
    /// <summary>The session of the task started, or started again with an answer of the user.</summary>
    Started,

    /// <summary>The session used a tool.</summary>
    Tool,

    /// <summary>The session wrote or changed a file: <see cref="TaskLogEntry.Path"/> names it.</summary>
    File,

    /// <summary>A tool failed or was refused.</summary>
    ToolFailed,

    /// <summary>What the session said at the end of a turn.</summary>
    Said,

    /// <summary>The session ended.</summary>
    Ended,
}

/// <summary>One line of the log of a task (ADR 0026, section 5). What a session wrote is data.</summary>
/// <param name="TaskId">The task.</param>
/// <param name="At">When it happened.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Text">A short text: the tool and what it was used on, or what was said.</param>
/// <param name="Path">The file written, for <see cref="TaskLogKind.File"/>.</param>
public sealed record TaskLogEntry(string TaskId, DateTimeOffset At, TaskLogKind Kind, string Text, string? Path = null);

/// <summary>
/// The full log of the tasks (ADR 0026, section 5): who runs the work of a task writes what happens, and the
/// thread of the task shows it on request, with the files the task wrote. Kept on this computer only.
/// </summary>
public interface ITaskLog
{
    /// <summary>Raised with the task whose log changed; on any thread.</summary>
    event Action<string>? Changed;

    /// <summary>Adds a line. A log that is full, or a store that fails, never stops the work: the line is dropped.</summary>
    /// <param name="entry">The line.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that ends once the line is kept or dropped.</returns>
    ValueTask AppendAsync(TaskLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Reads the log of a task, oldest line first.</summary>
    /// <param name="taskId">The task.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>Its lines; empty when nothing was written.</returns>
    ValueTask<IReadOnlyList<TaskLogEntry>> ReadAsync(string taskId, CancellationToken cancellationToken = default);
}
