using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.ClaudeCode;

/// <summary>
/// The work handed to Claude Code (US2.9; ADR 0020, section 1; ADR 0021, sections 1 and 8). The conversation slice
/// asks; the slice that starts the sessions answers: it chooses the folder, creates the task, starts the session
/// and follows it. Without an implementation a hand-off of kind « task » has no target.
/// </summary>
public interface IClaudeTasks
{
    /// <summary>The tasks whose session runs now, started by Jarvis.</summary>
    IReadOnlyList<string> Running { get; }

    /// <summary>
    /// When Jarvis asked on which folder a work must be done and <paramref name="request"/> answers it by naming
    /// one allowed folder: the work that waited, with its folder. Any other request forgets the question.
    /// Compared by code, never by a model.
    /// </summary>
    /// <param name="request">The request of the new turn, as heard or typed.</param>
    /// <returns>The work to start, or <see langword="null"/>.</returns>
    ClaudeTaskRequest? Resume(string request);

    /// <summary>
    /// Starts a session for <paramref name="request"/>. The folder is said before the launch
    /// (« Je lance Claude Code sur Kazo Bar. ») through <paramref name="announce"/>; an interruption during that
    /// sentence cancels the launch.
    /// </summary>
    /// <param name="request">The work.</param>
    /// <param name="announce">Says one sentence to the user now.</param>
    /// <param name="cancellationToken">An interruption by the user, until the session started.</param>
    /// <returns>Whether a session started, and the sentence that tells what happened.</returns>
    ValueTask<ClaudeTaskOpening> StartAsync(ClaudeTaskRequest request, Func<string, ValueTask> announce, CancellationToken cancellationToken = default);

    /// <summary>Stops the session of a task: the task becomes <c>abandoned</c>.</summary>
    /// <param name="taskId">The task.</param>
    /// <returns>Whether a session was running for it.</returns>
    ValueTask<bool> StopAsync(string taskId);
}

/// <summary>A work to hand to Claude Code.</summary>
/// <param name="Request">The initial request, word for word.</param>
/// <param name="Language">The language of the turn, for what Jarvis says.</param>
public sealed record ClaudeTaskRequest(string Request, AssistantLanguage Language)
{
    /// <summary>
    /// Makes the summary of the exchanges before the request, presented to the session as a context (ADR 0021,
    /// section 8). Asked only once a session is about to start; <see langword="null"/>, or a <see langword="null"/>
    /// result, when there is nothing to summarize.
    /// </summary>
    public Func<CancellationToken, ValueTask<string?>>? Context { get; init; }

    /// <summary>The folder, once chosen; <see langword="null"/> to let the request name it.</summary>
    public string? Folder { get; init; }
}

/// <summary>The answer of <see cref="IClaudeTasks.StartAsync"/>.</summary>
/// <param name="Started">Whether a session started.</param>
/// <param name="Said">When nothing started: why, or the question asked, in the language of the turn. When it started: what the model that handed off is told.</param>
/// <param name="TaskId">The task of the session.</param>
public sealed record ClaudeTaskOpening(bool Started, string Said, string? TaskId = null);

/// <summary>How a session started by Jarvis ended (ADR 0021, section 5).</summary>
public enum ClaudeTaskEnd
{
    /// <summary>Its work is over: the task is <c>done</c>.</summary>
    Done,

    /// <summary>It stopped without a result, or was stopped because a permission did not arrive: the task is <c>blocked</c>.</summary>
    Blocked,

    /// <summary>The user stopped it: the task is <c>abandoned</c>.</summary>
    Abandoned,
}

/// <summary>
/// A session started by Jarvis ended. Published once per session; whoever announces the sessions or counts their
/// cost listens. What a session wrote is data.
/// </summary>
/// <param name="TaskId">The task.</param>
/// <param name="Folder">The folder of the work.</param>
/// <param name="End">How it ended.</param>
/// <param name="Summary">Its last message, or the cause when it is blocked.</param>
/// <param name="CostUsd">The cost Claude Code announced for the session, in US dollars.</param>
/// <param name="Language">The language of the turn that started it.</param>
/// <param name="At">When it ended.</param>
public sealed record ClaudeTaskEnded(string TaskId, string Folder, ClaudeTaskEnd End, string? Summary, decimal? CostUsd, AssistantLanguage Language, DateTimeOffset At) : IEvent;
