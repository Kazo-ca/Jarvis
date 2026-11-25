using Kazo.Jarvis.Kernel.Conversation;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>
/// The messages of the threads of the subjects (ADR 0026, section 4), read from the journal of the conversation:
/// a message written in a thread is a turn that carries the address of that thread.
/// </summary>
public sealed class SubjectThreads : ISubjectThreads, IDisposable
{
    private readonly ConversationJournal journal;

    /// <summary>Creates the reader.</summary>
    /// <param name="journal">The turns of the conversation.</param>
    public SubjectThreads(ConversationJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        this.journal = journal;
        journal.Changed += OnChanged;
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public Task LoadAsync(CancellationToken cancellationToken = default) => journal.EnsureLoadedAsync(cancellationToken);

    /// <inheritdoc />
    public IReadOnlyList<ThreadMessage> Of(string project, string subject) =>
        [.. All.Where(message => message.Address.Project == project && message.Address.Subject == subject)];

    /// <inheritdoc />
    public IReadOnlyList<ThreadMessage> All =>
        [.. journal.Turns
            .Where(turn => turn.Thread is not null)
            .OrderBy(turn => turn.At)
            .Select(turn => new ThreadMessage(turn.Key, turn.At, turn.Request, turn.Answer, turn.Agent, turn.Thread!, turn.Outcome == TurnOutcome.Pending))];

    /// <inheritdoc />
    public void Dispose() => journal.Changed -= OnChanged;

    private void OnChanged() => Changed?.Invoke();
}
