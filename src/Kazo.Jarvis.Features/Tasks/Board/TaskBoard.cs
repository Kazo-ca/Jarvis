using Kazo.Jarvis.Kernel.Storage;
using Kazo.Jarvis.Kernel.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Tasks.Board;

/// <summary>
/// The board of the tasks (ADR 0013, section 6; ADR 0021, section 7): kept in memory while Jarvis runs and written
/// through to the collection <c>Tasks.Board</c> of the document store, one document per task. The tasks in memory
/// are the truth of a running Jarvis: « Tout effacer » empties the store, and a task still in progress stays on
/// the board until Jarvis stops.
/// </summary>
public sealed partial class TaskBoard : ITaskBoard, IHostedService, IDisposable
{
    /// <summary>The collection of the tasks.</summary>
    public const string Collection = "Tasks.Board";

    /// <summary>Most characters of a title.</summary>
    public const int MostTitle = 200;

    /// <summary>Most characters of a summary: a last message is shortened, never kept whole.</summary>
    public const int MostSummary = 2000;

    private readonly IDocumentStore store;
    private readonly TimeProvider time;
    private readonly ILogger<TaskBoard> logger;
    private readonly SemaphoreSlim writing = new(1, 1);
    private readonly Lock gate = new();
    private readonly Dictionary<string, (WorkTask Task, DocumentId? Document)> tasks = new(StringComparer.Ordinal);
    private bool loaded;

    /// <summary>Creates the board. The stored tasks are read when Jarvis starts.</summary>
    /// <param name="store">The store of the documents.</param>
    /// <param name="time">The clock of the dates.</param>
    /// <param name="logger">Logs the store failures and the moves, never a summary.</param>
    public TaskBoard(IDocumentStore store, TimeProvider time, ILogger<TaskBoard> logger)
    {
        this.store = store;
        this.time = time;
        this.logger = logger;
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<WorkTask> Tasks
    {
        get
        {
            lock (gate)
            {
                return [.. tasks.Values.Select(entry => entry.Task).OrderByDescending(task => task.UpdatedAt).ThenBy(task => task.Id, StringComparer.Ordinal)];
            }
        }
    }

    /// <inheritdoc />
    public WorkTask? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (gate)
        {
            return tasks.TryGetValue(id, out var entry) ? entry.Task : null;
        }
    }

    /// <inheritdoc />
    public WorkTask? FindBySession(string account, string session)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(session);
        lock (gate)
        {
            return tasks.Values
                .Select(entry => entry.Task)
                .Where(task => task.ParentId is null && task.Operator.Account == account && task.Operator.Session == session)
                .OrderByDescending(task => task.UpdatedAt)
                .FirstOrDefault();
        }
    }

    /// <inheritdoc />
    public async ValueTask<WorkTask> CreateAsync(NewWorkTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var now = time.GetUtcNow();
        WorkTask created;
        lock (gate)
        {
            // One level only (ADR 0013, section 6): the sub-task of a sub-task hangs on the same parent.
            var parent = task.ParentId is { } wanted && tasks.TryGetValue(wanted, out var found) ? found.Task : null;
            created = new WorkTask(
                "task-" + Guid.CreateVersion7(now).ToString("N"),
                parent is null ? null : parent.ParentId ?? parent.Id,
                Clean(task.Project, MostTitle) is { Length: > 0 } project ? project : TaskKeys.Inbox,
                Clean(task.Topic, MostTitle) is { Length: > 0 } topic ? topic : TaskKeys.Inbox,
                Clean(task.Title, MostTitle),
                null,
                task.Operator,
                task.Source,
                task.Status,
                now,
                now,
                task.Folder);
            tasks[created.Id] = (created, null);
        }

        LogCreated(created.Id, created.Source.ToWire(), created.Status.ToWire());
        await KeepAsync(created.Id, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke();
        return created;
    }

    /// <inheritdoc />
    public async ValueTask<WorkTask?> ChangeAsync(string id, TaskChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(change);
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        WorkTask? after;
        lock (gate)
        {
            if (!tasks.TryGetValue(id, out var entry))
            {
                return null;
            }

            var before = entry.Task;
            if (before.Ended)
            {
                // A hook that arrives after the end does not bring a task back.
                return before;
            }

            after = before with
            {
                Status = change.Status ?? before.Status,
                Summary = change.Summary is null ? before.Summary : Clean(change.Summary, MostSummary),
                Title = change.Title is null ? before.Title : Clean(change.Title, MostTitle),
                Operator = change.Session is null ? before.Operator : before.Operator with { Session = change.Session },
            };
            if (after == before)
            {
                return before;
            }

            after = after with { UpdatedAt = time.GetUtcNow() };
            tasks[id] = (after, entry.Document);
            if (after.Status != before.Status)
            {
                LogMoved(id, before.Status.ToWire(), after.Status.ToWire());
            }
        }

        await KeepAsync(id, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke();
        return after;
    }

    /// <inheritdoc />
    public async ValueTask<int> RemoveEndedAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        List<DocumentId?> documents = [];
        lock (gate)
        {
            var ended = tasks.Values
                .Where(entry => entry.Task.ParentId is null && entry.Task.Ended && entry.Task.UpdatedAt < before)
                .Select(entry => entry.Task.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var (id, entry) in tasks.Where(pair => ended.Contains(pair.Key) || (pair.Value.Task.ParentId is { } parent && ended.Contains(parent))).ToList())
            {
                documents.Add(entry.Document);
                tasks.Remove(id);
            }
        }

        if (documents.Count == 0)
        {
            return 0;
        }

        await writing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var document in documents.OfType<DocumentId>())
            {
                await Documents().DeleteAsync(document, cancellationToken).ConfigureAwait(false);
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

        Changed?.Invoke();
        return documents.Count;
    }

    /// <summary>Reads the stored tasks, so the board is whole before the first hook.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>A task that ends once the board is read.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose() => writing.Dispose();

    private static string Clean(string? text, int most)
    {
        var clean = (text ?? string.Empty).Trim();
        return clean.Length <= most ? clean : clean[..(most - 1)].TrimEnd() + "…";
    }

    private async ValueTask EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref loaded))
        {
            return;
        }

        await writing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (loaded)
            {
                return;
            }

            var stored = await Documents().ReadPeriodAsync(DateTimeOffset.UnixEpoch, time.GetUtcNow().AddDays(1), cancellationToken).ConfigureAwait(false);
            lock (gate)
            {
                // Nothing was created before this reading. The newest document of a task wins: a change writes
                // the new one before it removes the old one, and Jarvis may have stopped between the two.
                foreach (var document in stored.OrderBy(document => document.Content.UpdatedAt))
                {
                    tasks[document.Content.Id] = (document.Content, document.Id);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A store that fails never stops the tasks from being followed: the board works in memory.
            LogStoreFailed(exception);
        }
        finally
        {
            Volatile.Write(ref loaded, true);
            writing.Release();
        }
    }

    /// <summary>Writes the task as it is now, then removes its older document.</summary>
    private async ValueTask KeepAsync(string id, CancellationToken cancellationToken)
    {
        await writing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WorkTask task;
            DocumentId? old;
            lock (gate)
            {
                if (!tasks.TryGetValue(id, out var entry))
                {
                    return;
                }

                (task, old) = entry;
            }

            var kept = await Documents().AddAsync(task, task.CreatedAt, cancellationToken).ConfigureAwait(false);
            lock (gate)
            {
                if (tasks.TryGetValue(id, out var entry))
                {
                    tasks[id] = (entry.Task, kept.Id);
                }
            }

            if (old is { } previous)
            {
                await Documents().DeleteAsync(previous, cancellationToken).ConfigureAwait(false);
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
    }

    private IDocumentCollection<WorkTask> Documents() => store.Collection<WorkTask>(Collection);

    [LoggerMessage(Level = LogLevel.Information, Message = "Task {Task} created ({Source}, {Status}).")]
    private partial void LogCreated(string task, string source, string status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Task {Task}: {Before} -> {After}.")]
    private partial void LogMoved(string task, string before, string after);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The tasks could not be read from or written to the store.")]
    private partial void LogStoreFailed(Exception exception);
}
