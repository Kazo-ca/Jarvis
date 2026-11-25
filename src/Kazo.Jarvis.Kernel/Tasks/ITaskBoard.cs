namespace Kazo.Jarvis.Kernel.Tasks;

/// <summary>
/// The tasks Jarvis follows (ADR 0013, section 6; ADR 0021, section 7). The board slice keeps them in the document
/// store; whoever starts a session, hears a hook or draws the fan reads and moves them here.
/// </summary>
public interface ITaskBoard
{
    /// <summary>Raised after a task was created, moved or removed; on any thread.</summary>
    event Action? Changed;

    /// <summary>The tasks, the most recently changed first.</summary>
    IReadOnlyList<WorkTask> Tasks { get; }

    /// <summary>The task <paramref name="id"/>.</summary>
    /// <param name="id">An identifier.</param>
    /// <returns>The task, or <see langword="null"/>.</returns>
    WorkTask? Find(string id);

    /// <summary>The task whose operator is the session <paramref name="session"/> of <paramref name="account"/>, the most recent one.</summary>
    /// <param name="account">The account of the operator.</param>
    /// <param name="session">The session.</param>
    /// <returns>The task, or <see langword="null"/>.</returns>
    WorkTask? FindBySession(string account, string session);

    /// <summary>Creates a task.</summary>
    /// <param name="task">What is known of it.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <returns>The task, with its identifier.</returns>
    ValueTask<WorkTask> CreateAsync(NewWorkTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a task. A task that ended (<c>done</c>, <c>abandoned</c>) no longer changes: what arrives late for it
    /// is ignored.
    /// </summary>
    /// <param name="id">The task.</param>
    /// <param name="change">What changes; <see langword="null"/> members keep their value.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <returns>The task after the change; <see langword="null"/> when there is no such task.</returns>
    ValueTask<WorkTask?> ChangeAsync(string id, TaskChange change, CancellationToken cancellationToken = default);

    /// <summary>Removes the tasks that ended before <paramref name="before"/>, and their sub-tasks.</summary>
    /// <param name="before">The limit.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <returns>How many were removed.</returns>
    ValueTask<int> RemoveEndedAsync(DateTimeOffset before, CancellationToken cancellationToken = default);
}

/// <summary>What changes in a task.</summary>
/// <param name="Status">The new status.</param>
/// <param name="Summary">The new summary.</param>
/// <param name="Session">The session of the operator, once known.</param>
/// <param name="Title">The new title.</param>
public sealed record TaskChange(WorkTaskStatus? Status = null, string? Summary = null, string? Session = null, string? Title = null)
{
    /// <summary>
    /// Brings a task that is over back to work (ADR 0026, section 4): asked only by who starts its session again
    /// with an answer of the user. Without it a change that arrives after the end is ignored.
    /// </summary>
    public bool Reopen { get; init; }
}
