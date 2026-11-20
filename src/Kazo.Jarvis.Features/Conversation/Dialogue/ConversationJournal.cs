using Kazo.Jarvis.Kernel.Storage;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>
/// The conversation (US1.14): the turns kept in the collection <c>Conversation.Turns</c> (ADR 0004, section 3) and
/// the turn in progress, for the window and for the history sent to the model. The store is read once, on first
/// use. When it cannot be used, the conversation still works in memory and the log says why.
/// </summary>
public sealed partial class ConversationJournal
{
    /// <summary>The collection of this slice.</summary>
    public const string CollectionName = "Conversation.Turns";

    /// <summary>Turns kept in memory for the window; older ones stay in the store.</summary>
    public const int KeptInMemory = 500;

    private readonly IDocumentStore store;
    private readonly TimeProvider time;
    private readonly ILogger<ConversationJournal> logger;
    private readonly Lock gate = new();
    private readonly SemaphoreSlim loading = new(1, 1);
    private List<ConversationTurn> turns = [];
    private bool loaded;

    /// <summary>Creates the journal.</summary>
    /// <param name="store">The store of the documents.</param>
    /// <param name="time">The clock of the period read.</param>
    /// <param name="logger">Logs the store failures, never the text of the conversation (ADR 0004, section 6).</param>
    public ConversationJournal(IDocumentStore store, TimeProvider time, ILogger<ConversationJournal> logger)
    {
        this.store = store;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>Raised after every change, on any thread.</summary>
    public event Action? Changed;

    /// <summary>The turns, oldest first, with the turn in progress last.</summary>
    public IReadOnlyList<ConversationTurn> Turns
    {
        get
        {
            lock (gate)
            {
                return [.. turns];
            }
        }
    }

    /// <summary>Whether the stored turns have been read.</summary>
    public bool IsLoaded
    {
        get
        {
            lock (gate)
            {
                return loaded;
            }
        }
    }

    /// <summary>Reads the stored turns once. Later calls return at once.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the turns are in memory.</returns>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoaded)
        {
            return;
        }

        await loading.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsLoaded)
            {
                return;
            }

            IReadOnlyList<StoredDocument<ConversationTurn>> stored = [];
            try
            {
                stored = await Collection().ReadPeriodAsync(DateTimeOffset.UnixEpoch, time.GetUtcNow().AddDays(1), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogReadFailed(exception);
            }

            lock (gate)
            {
                var live = turns;
                var keys = live.Select(turn => turn.Key).ToHashSet();
                turns = [.. stored.Select(document => document.Content).Where(turn => !keys.Contains(turn.Key)).Concat(live).OrderBy(turn => turn.At)];
                Trim();
                loaded = true;
            }
        }
        finally
        {
            loading.Release();
        }

        Changed?.Invoke();
    }

    /// <summary>Adds the turn or replaces the one with the same <see cref="ConversationTurn.Key"/>; not stored yet.</summary>
    /// <param name="turn">The turn.</param>
    public void Show(ConversationTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        lock (gate)
        {
            var index = turns.FindIndex(existing => existing.Key == turn.Key);
            if (index >= 0)
            {
                turns[index] = turn;
            }
            else
            {
                turns.Add(turn);
                Trim();
            }
        }

        Changed?.Invoke();
    }

    /// <summary>Shows the finished turn and adds it to the store.</summary>
    /// <param name="turn">The finished turn.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when it is stored, or when storing failed (logged).</returns>
    public async Task SaveAsync(ConversationTurn turn, CancellationToken cancellationToken = default)
    {
        Show(turn);
        try
        {
            await Collection().AddAsync(turn, turn.At, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogWriteFailed(exception);
        }
    }

    /// <summary>
    /// The « Tout effacer » button (ADR 0004, section 3): erases the conversation, the memory and the log of the
    /// store, and empties the window. Settings and secrets are not touched.
    /// </summary>
    /// <param name="cancellationToken">Cancels the erasure before it starts.</param>
    /// <returns>A task that completes when everything is erased.</returns>
    public async Task EraseAllAsync(CancellationToken cancellationToken = default)
    {
        await store.EraseAllAsync(cancellationToken).ConfigureAwait(false);
        lock (gate)
        {
            // The turn in progress stays on screen: it is stored when it ends.
            turns = [.. turns.Where(turn => turn.Outcome == TurnOutcome.Pending)];
        }

        LogErased();
        Changed?.Invoke();
    }

    private IDocumentCollection<ConversationTurn> Collection() => store.Collection<ConversationTurn>(CollectionName);

    private void Trim()
    {
        if (turns.Count > KeptInMemory)
        {
            turns.RemoveRange(0, turns.Count - KeptInMemory);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The stored conversation could not be read; the window starts empty.")]
    private partial void LogReadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A turn of the conversation could not be stored.")]
    private partial void LogWriteFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "The conversation, the memory and the log were erased.")]
    private partial void LogErased();
}
