using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Storage;
using Kazo.Jarvis.Kernel.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Features.Tasks.Journal;

/// <summary>
/// The log of the tasks (ADR 0026, section 5), one document per line in the collection <c>Tasks.Log</c>. A log
/// holds at most <see cref="MostEntries"/> lines of at most <see cref="MostText"/> characters; a text that looks
/// like a secret is not kept. The log of a task that left the board is erased with it.
/// </summary>
public sealed partial class TaskLog : ITaskLog, IDisposable
{
    /// <summary>The collection of the lines.</summary>
    public const string Collection = "Tasks.Log";

    /// <summary>The most lines kept for one task.</summary>
    public const int MostEntries = 500;

    /// <summary>The most characters of a line.</summary>
    public const int MostText = 500;

    /// <summary>What replaces a text that looks like a secret.</summary>
    public const string Masked = "[…]";

    private readonly IDocumentStore store;
    private readonly ITaskBoard? board;
    private readonly ILogger<TaskLog> logger;
    private readonly Lock gate = new();
    private readonly SemaphoreSlim loading = new(1, 1);
    private readonly Dictionary<string, List<StoredDocument<TaskLogEntry>>> logs = new(StringComparer.Ordinal);
    private bool loaded;

    /// <summary>Creates the log.</summary>
    /// <param name="store">The document store.</param>
    /// <param name="board">The tasks, when the host has them: the log of a task that is removed is erased.</param>
    /// <param name="logger">The log of the application. Never what a session wrote.</param>
    public TaskLog(IDocumentStore store, ITaskBoard? board = null, ILogger<TaskLog>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
        this.board = board;
        this.logger = logger ?? NullLogger<TaskLog>.Instance;
        if (board is not null)
        {
            board.Changed += OnBoardChanged;
        }
    }

    /// <inheritdoc />
    public event Action<string>? Changed;

    /// <inheritdoc />
    public async ValueTask AppendAsync(TaskLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            lock (gate)
            {
                if (logs.TryGetValue(entry.TaskId, out var known) && known.Count >= MostEntries)
                {
                    return;
                }
            }

            var kept = entry with { Text = Clean(entry.Text), Path = entry.Path is null ? null : Clean(entry.Path) };
            var stored = await store.Collection<TaskLogEntry>(Collection).AddAsync(kept, kept.At, cancellationToken).ConfigureAwait(false);
            lock (gate)
            {
                if (!logs.TryGetValue(entry.TaskId, out var lines))
                {
                    logs[entry.TaskId] = lines = [];
                }

                lines.Add(stored);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStoreFailed(exception);
            return;
        }

        Changed?.Invoke(entry.TaskId);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<TaskLogEntry>> ReadAsync(string taskId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogStoreFailed(exception);
            return [];
        }

        lock (gate)
        {
            return logs.TryGetValue(taskId, out var lines) ? [.. lines.Select(line => line.Content)] : [];
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (board is not null)
        {
            board.Changed -= OnBoardChanged;
        }

        loading.Dispose();
    }

    private static string Clean(string? text)
    {
        var line = string.Join(' ', (text ?? string.Empty).Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (SecretShapes.LooksLikeSecret(line))
        {
            return Masked;
        }

        return line.Length <= MostText ? line : line[..(MostText - 1)] + "…";
    }

    private async ValueTask EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (loaded)
        {
            return;
        }

        await loading.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (loaded)
            {
                return;
            }

            var all = await store.Collection<TaskLogEntry>(Collection).ReadPeriodAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, cancellationToken).ConfigureAwait(false);
            lock (gate)
            {
                foreach (var group in all.GroupBy(line => line.Content.TaskId, StringComparer.Ordinal))
                {
                    logs[group.Key] = [.. group.OrderBy(line => line.At)];
                }

                loaded = true;
            }
        }
        finally
        {
            loading.Release();
        }
    }

    // A task that left the board takes its log with it. An empty board says nothing: it may not be read yet.
    private void OnBoardChanged()
    {
        var tasks = board!.Tasks;
        if (tasks.Count == 0 || !loaded)
        {
            return;
        }

        var known = tasks.Select(task => task.Id).ToHashSet(StringComparer.Ordinal);
        List<StoredDocument<TaskLogEntry>> gone;
        lock (gate)
        {
            var left = logs.Keys.Where(task => !known.Contains(task)).ToList();
            gone = [.. left.SelectMany(task => logs[task])];
            foreach (var task in left)
            {
                logs.Remove(task);
            }
        }

        if (gone.Count > 0)
        {
            _ = EraseAsync(gone);
        }
    }

    private async Task EraseAsync(List<StoredDocument<TaskLogEntry>> gone)
    {
        try
        {
            var documents = store.Collection<TaskLogEntry>(Collection);
            foreach (var line in gone)
            {
                await documents.DeleteAsync(line.Id).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            LogStoreFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The log of the tasks could not be read from or written to the store.")]
    private partial void LogStoreFailed(Exception exception);
}
