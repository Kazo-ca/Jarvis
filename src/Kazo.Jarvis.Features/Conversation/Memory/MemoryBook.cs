using System.Text.RegularExpressions;
using Kazo.Jarvis.Kernel.Storage;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Conversation.Memory;

/// <summary>
/// What Jarvis keeps (ADR 0019, sections 1 and 7): the facts and the one summary, in two collections of the
/// document store. Nothing is kept in memory between two reads: the « Tout effacer » button of the conversation
/// erases the whole store, and the next read sees it.
/// </summary>
public sealed partial class MemoryBook
{
    /// <summary>The collection of the facts. ADR 0019 writes <c>Conversation.Memory.Facts</c>; a collection name has one dot.</summary>
    public const string FactsCollection = "Conversation.MemoryFacts";

    /// <summary>The collection of the summary.</summary>
    public const string SummaryCollection = "Conversation.MemorySummary";

    /// <summary>Most facts kept.</summary>
    public const int MostFacts = 500;

    private readonly IDocumentStore store;
    private readonly TimeProvider time;
    private readonly ILogger<MemoryBook> logger;
    private readonly SemaphoreSlim writing = new(1, 1);

    /// <summary>Creates the book.</summary>
    /// <param name="store">The store of the documents.</param>
    /// <param name="time">The clock of the dates.</param>
    /// <param name="logger">Logs the store failures, never a fact (ADR 0004, section 6).</param>
    public MemoryBook(IDocumentStore store, TimeProvider time, ILogger<MemoryBook> logger)
    {
        this.store = store;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>Raised after every change, on any thread.</summary>
    public event Action? Changed;

    /// <summary>A fact as it is compared and kept: one line, no bracket that could close the block of the memory.</summary>
    /// <param name="text">The text given.</param>
    /// <returns>The text kept.</returns>
    public static string Clean(string? text) =>
        Spaces().Replace(text ?? string.Empty, " ").Replace('[', '(').Replace(']', ')').Trim();

    /// <summary>The facts, most recent first.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts; empty when the store cannot be read.</returns>
    public async ValueTask<IReadOnlyList<MemoryFact>> FactsAsync(CancellationToken cancellationToken = default) =>
        [.. (await ReadAsync(cancellationToken).ConfigureAwait(false)).Select(document => document.Content).OrderByDescending(fact => fact.At)];

    /// <summary>Keeps a fact, unless it is empty, too long, already known or shaped like a secret.</summary>
    /// <param name="text">The sentence.</param>
    /// <param name="source">Where it comes from.</param>
    /// <param name="turn">The stored turn it comes from.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>What happened.</returns>
    public ValueTask<FactOutcome> AddAsync(string? text, FactSource source, Guid? turn = null, CancellationToken cancellationToken = default) =>
        AddAsync(null, text, source, turn, cancellationToken);

    /// <summary>Keeps a fact for one agent (ADR 0025, section 5), under the same rules as a fact about the user.</summary>
    /// <param name="agent">The identity of the agent, or <see langword="null"/> for a fact about the user.</param>
    /// <param name="text">The sentence.</param>
    /// <param name="source">Where it comes from.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>What happened.</returns>
    public ValueTask<FactOutcome> AddForAgentAsync(string? agent, string? text, FactSource source, CancellationToken cancellationToken = default) =>
        AddAsync(string.IsNullOrWhiteSpace(agent) ? null : agent.Trim(), text, source, null, cancellationToken);

    private async ValueTask<FactOutcome> AddAsync(string? agent, string? text, FactSource source, Guid? turn, CancellationToken cancellationToken)
    {
        var clean = Clean(text);
        if (clean.Length == 0)
        {
            return FactOutcome.Empty;
        }

        if (clean.Length > MemoryFact.MostCharacters)
        {
            return FactOutcome.TooLong;
        }

        if (SecretShapes.LooksLikeSecret(clean))
        {
            return FactOutcome.LooksLikeSecret;
        }

        await writing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var known = await Facts().ReadPeriodAsync(DateTimeOffset.UnixEpoch, time.GetUtcNow().AddDays(1), cancellationToken).ConfigureAwait(false);
            // The same sentence may serve two agents: it is known only where it was kept.
            if (known.Any(document => document.Content.Agent == agent && Same(document.Content.Text, clean)))
            {
                return FactOutcome.AlreadyKnown;
            }

            if (known.Count >= MostFacts)
            {
                return FactOutcome.Full;
            }

            var now = time.GetUtcNow();
            await Facts().AddAsync(new MemoryFact { Text = clean, Source = source, At = now, Turn = turn, Agent = agent }, now, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStoreFailed(exception);
            return FactOutcome.Unavailable;
        }
        finally
        {
            writing.Release();
        }

        Changed?.Invoke();
        return FactOutcome.Added;
    }

    /// <summary>Erases one fact.</summary>
    /// <param name="key">Its key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether it was there.</returns>
    public async ValueTask<bool> RemoveAsync(Guid key, CancellationToken cancellationToken = default)
    {
        var removed = false;
        await writing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var document in (await ReadAsync(cancellationToken).ConfigureAwait(false)).Where(document => document.Content.Key == key))
            {
                removed |= await Facts().DeleteAsync(document.Id, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStoreFailed(exception);
        }
        finally
        {
            writing.Release();
        }

        if (removed)
        {
            Changed?.Invoke();
        }

        return removed;
    }

    /// <summary>Pins a fact or takes its pin off. The store has no update: the fact is written again at its date.</summary>
    /// <param name="key">Its key.</param>
    /// <param name="pinned">The new state.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether the fact was there.</returns>
    public async ValueTask<bool> PinAsync(Guid key, bool pinned, CancellationToken cancellationToken = default)
    {
        var found = false;
        await writing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if ((await ReadAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(document => document.Content.Key == key) is { } document)
            {
                found = true;
                if (document.Content.Pinned != pinned)
                {
                    await Facts().AddAsync(document.Content with { Pinned = pinned }, document.Content.At, cancellationToken).ConfigureAwait(false);
                    await Facts().DeleteAsync(document.Id, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStoreFailed(exception);
        }
        finally
        {
            writing.Release();
        }

        if (found)
        {
            Changed?.Invoke();
        }

        return found;
    }

    /// <summary>The summary, or <see langword="null"/> when there is none.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The summary.</returns>
    public async ValueTask<MemorySummary?> SummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = await Summaries().ReadPeriodAsync(DateTimeOffset.UnixEpoch, time.GetUtcNow().AddDays(1), cancellationToken).ConfigureAwait(false);
            return stored.Select(document => document.Content).OrderByDescending(summary => summary.At).FirstOrDefault();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStoreFailed(exception);
            return null;
        }
    }

    /// <summary>Replaces the summary.</summary>
    /// <param name="text">The new summary.</param>
    /// <param name="through">The moment of the last exchange it holds.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when it is stored, or when storing failed (logged).</returns>
    public async ValueTask WriteSummaryAsync(string text, DateTimeOffset through, CancellationToken cancellationToken = default)
    {
        await writing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = time.GetUtcNow();
            await Summaries().ClearAsync(cancellationToken).ConfigureAwait(false);
            await Summaries().AddAsync(new MemorySummary { Text = text, Through = through, At = now }, now, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStoreFailed(exception);
        }
        finally
        {
            writing.Release();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Erases the text of the summary. The mark of what it held stays: the exchanges already summarized are not
    /// summarized again, so what the user erased does not come back.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when it is erased.</returns>
    public async ValueTask EraseSummaryAsync(CancellationToken cancellationToken = default)
    {
        var through = (await SummaryAsync(cancellationToken).ConfigureAwait(false))?.Through ?? time.GetUtcNow();
        var now = time.GetUtcNow();
        await WriteSummaryAsync(string.Empty, through > now ? through : now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Erases every fact and the summary: the « Tout effacer » button of the « Mémoire » page.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when everything is erased.</returns>
    public async ValueTask EraseAllAsync(CancellationToken cancellationToken = default)
    {
        await writing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Facts().ClearAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStoreFailed(exception);
        }
        finally
        {
            writing.Release();
        }

        await EraseSummaryAsync(cancellationToken).ConfigureAwait(false);
        LogErased();
    }

    /// <summary>Whether two facts are the same, whatever the case and the spaces (ADR 0019, section 4).</summary>
    internal static bool Same(string first, string second) =>
        string.Equals(Spaces().Replace(first, string.Empty), Spaces().Replace(second, string.Empty), StringComparison.OrdinalIgnoreCase);

    private async ValueTask<IReadOnlyList<StoredDocument<MemoryFact>>> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Facts().ReadPeriodAsync(DateTimeOffset.UnixEpoch, time.GetUtcNow().AddDays(1), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStoreFailed(exception);
            return [];
        }
    }

    private IDocumentCollection<MemoryFact> Facts() => store.Collection<MemoryFact>(FactsCollection);

    private IDocumentCollection<MemorySummary> Summaries() => store.Collection<MemorySummary>(SummaryCollection);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [LoggerMessage(Level = LogLevel.Warning, Message = "The memory could not be read or written in the store.")]
    private partial void LogStoreFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "The facts and the summary of the memory were erased.")]
    private partial void LogErased();
}
