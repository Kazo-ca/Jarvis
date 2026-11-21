using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Speech;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>
/// What Jarvis says without being asked (ADR 0024). An <see cref="AnnouncementToSay"/> is written in the
/// conversation as soon as it arrives, said or not. One to say aloud waits for the assistant to be at rest, is
/// then submitted to the state machine, and its sentences go to the voice when its turn starts. Whether an
/// announcement is said is decided by whoever publishes it; when it is said is decided here.
/// <para>
/// An announcement is never sent to a model: it is not a turn of the history (see
/// <see cref="ConversationEngine.Recent"/>).
/// </para>
/// </summary>
public sealed class AnnouncementDesk :
    IEventHandler<AnnouncementToSay>,
    IEventHandler<AssistantStateChanged>,
    IEventHandler<TurnStarted>
{
    /// <summary>Most announcements waiting to be said; beyond that the oldest ones stay written only.</summary>
    public const int MostWaiting = 5;

    /// <summary>How long an announcement waits for the rest before it is no longer said.</summary>
    public static readonly TimeSpan LongestWait = TimeSpan.FromMinutes(10);

    private readonly ConversationJournal journal;
    private readonly IAssistantStateReader state;
    private readonly IMessageBus bus;
    private readonly TimeProvider time;
    private readonly Lock gate = new();
    private readonly LinkedList<Waiting> waiting = new();

    /// <summary>Creates the desk.</summary>
    /// <param name="journal">The conversation the announcements are written in.</param>
    /// <param name="state">The state of the assistant, to know when it is at rest.</param>
    /// <param name="bus">The bus, for the state machine and the voice.</param>
    /// <param name="time">The clock of the waits.</param>
    public AnnouncementDesk(ConversationJournal journal, IAssistantStateReader state, IMessageBus bus, TimeProvider time)
    {
        this.journal = journal;
        this.state = state;
        this.bus = bus;
        this.time = time;
    }

    /// <summary>How many announcements wait to be said; for the tests.</summary>
    internal int WaitingCount
    {
        get
        {
            lock (gate)
            {
                return waiting.Count;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask HandleAsync(AnnouncementToSay message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var text = message.Text.Trim();
        var now = time.GetUtcNow();
        await journal.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await journal.SaveAsync(
            new ConversationTurn
            {
                At = now,
                Input = TurnInput.Announcement,
                Answer = text,
                Language = message.Language.Code,
                Outcome = TurnOutcome.Answered,
            },
            CancellationToken.None).ConfigureAwait(false);
        if (!message.Aloud)
        {
            return;
        }

        lock (gate)
        {
            waiting.AddLast(new Waiting(text, now));
            while (waiting.Count > MostWaiting)
            {
                waiting.RemoveFirst();
            }
        }

        await SubmitAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(AssistantStateChanged message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message.Current.Phase == AssistantPhase.Idle ? SubmitAsync() : ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask HandleAsync(TurnStarted message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Input != TurnInput.Announcement || message.Text is not { } text)
        {
            return;
        }

        lock (gate)
        {
            // The turn that starts is the one of the oldest announcement submitted with this text.
            for (var node = waiting.First; node is not null; node = node.Next)
            {
                if (string.Equals(node.Value.Text, text, StringComparison.Ordinal))
                {
                    waiting.Remove(node);
                    break;
                }
            }
        }

        var splitter = new SentenceSplitter();
        foreach (var sentence in splitter.Push(text).Concat(splitter.Flush()))
        {
            await bus.PublishAsync(new SentenceToSpeak(message.Turn, sentence), CancellationToken.None).ConfigureAwait(false);
        }

        // Nothing speakable (marks only): the voice then ends the turn without a sound.
        await bus.PublishAsync(new AnswerSentencesEnded(message.Turn), CancellationToken.None).ConfigureAwait(false);
    }

    private async ValueTask SubmitAsync()
    {
        if (state.Current.Phase != AssistantPhase.Idle)
        {
            return;
        }

        string? next;
        var now = time.GetUtcNow();
        lock (gate)
        {
            // Too old to be said: it stays written in the conversation.
            while (waiting.First is { } first && now - first.Value.At > LongestWait)
            {
                waiting.RemoveFirst();
            }

            next = waiting.First?.Value.Text;
        }

        if (next is not null)
        {
            // Ignored by the state machine when a turn started meanwhile: the next rest submits it again.
            await bus.PublishAsync<AssistantInput>(new AnnouncementSubmitted(next), CancellationToken.None).ConfigureAwait(false);
        }
    }

    private readonly record struct Waiting(string Text, DateTimeOffset At);
}
