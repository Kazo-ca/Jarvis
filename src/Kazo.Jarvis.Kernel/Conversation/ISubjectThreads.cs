namespace Kazo.Jarvis.Kernel.Conversation;

/// <summary>
/// Where a typed request was written, when it was not the conversation window: the thread of a subject of the
/// projects view (ADR 0026, section 4). A turn that carries one is answered by the agent of the project and is
/// shown in that thread.
/// </summary>
/// <param name="Project">The identifier of the project in the view; the built-in project has its own.</param>
/// <param name="Subject">The key of the subject in the project.</param>
/// <param name="Task">The task the message answers (<c>T3&gt;</c>), or <see langword="null"/>.</param>
/// <param name="TaskLabel">The label of that task when the message was written: <c>T3</c>.</param>
/// <param name="TaskTitle">The title of that task, told to the agent that answers.</param>
public sealed record ThreadAddress(string Project, string Subject, string? Task = null, string? TaskLabel = null, string? TaskTitle = null);

/// <summary>An exchange written in the thread of a subject.</summary>
/// <param name="Key">The key of the turn in the conversation.</param>
/// <param name="At">When the user wrote.</param>
/// <param name="Request">What the user wrote.</param>
/// <param name="Answer">What was answered so far; empty while the answer is not there.</param>
/// <param name="Agent">The name of the agent that answered, or <see langword="null"/>.</param>
/// <param name="Address">Where it was written.</param>
/// <param name="Pending">Whether the answer is still being written.</param>
public sealed record ThreadMessage(Guid Key, DateTimeOffset At, string Request, string Answer, string? Agent, ThreadAddress Address, bool Pending);

/// <summary>
/// The messages of the threads of the subjects, for the projects view (ADR 0026, sections 1 and 4). The
/// conversation slice keeps them: a message written in a thread is a turn of the conversation.
/// </summary>
public interface ISubjectThreads
{
    /// <summary>Raised when a message was added or changed, on any thread.</summary>
    event Action? Changed;

    /// <summary>Reads what was kept before Jarvis started; the messages are empty until then.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>A task that completes when the messages are read.</returns>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>The messages of a subject, oldest first.</summary>
    /// <param name="project">The identifier of the project in the view.</param>
    /// <param name="subject">The key of the subject.</param>
    /// <returns>The messages.</returns>
    IReadOnlyList<ThreadMessage> Of(string project, string subject);
}
