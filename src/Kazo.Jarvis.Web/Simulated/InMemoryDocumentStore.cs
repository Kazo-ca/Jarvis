using System.Text.Json;
using Kazo.Jarvis.Kernel.Storage;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated document store for the Web host (ADR 0004, section 3): same contract as the SQLite store, in
/// memory. Documents are kept as JSON and times to the millisecond, like in the database.
/// </summary>
public sealed class InMemoryDocumentStore(TimeProvider time) : IDocumentStore
{
    private readonly TimeProvider time = time;
    private readonly Lock gate = new();
    private readonly List<Row> rows = [];
    private long sequence;

    /// <inheritdoc />
    public IDocumentCollection<T> Collection<T>(string name)
        where T : class
    {
        DocumentCollections.EnsureValidName(name);
        return new MemoryCollection<T>(this, name);
    }

    /// <inheritdoc />
    public ValueTask EraseAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            rows.Clear();
        }

        return ValueTask.CompletedTask;
    }

    private sealed record Row(long Sequence, DocumentId Id, string Collection, long At, string Content);

    private sealed class MemoryCollection<T>(InMemoryDocumentStore store, string name) : IDocumentCollection<T>
        where T : class
    {
        public string Name => name;

        public ValueTask<StoredDocument<T>> AddAsync(T document, DateTimeOffset? at = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(document);
            var milliseconds = (at ?? store.time.GetUtcNow()).ToUnixTimeMilliseconds();
            var when = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            var id = DocumentId.New(when);
            lock (store.gate)
            {
                store.rows.Add(new Row(++store.sequence, id, name, milliseconds, JsonSerializer.Serialize(document, JsonSerializerOptions.Web)));
            }

            return ValueTask.FromResult(new StoredDocument<T>(id, when, document));
        }

        public ValueTask<StoredDocument<T>?> GetAsync(DocumentId id, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Select(row => row.Id == id).FirstOrDefault());

        public ValueTask<IReadOnlyList<StoredDocument<T>>> ReadPeriodAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        {
            var start = from.ToUnixTimeMilliseconds();
            var end = to.ToUnixTimeMilliseconds();
            return ValueTask.FromResult<IReadOnlyList<StoredDocument<T>>>(Select(row => row.At >= start && row.At < end));
        }

        public ValueTask<bool> DeleteAsync(DocumentId id, CancellationToken cancellationToken = default)
        {
            lock (store.gate)
            {
                return ValueTask.FromResult(store.rows.RemoveAll(row => row.Collection == name && row.Id == id) > 0);
            }
        }

        public ValueTask<int> ClearAsync(CancellationToken cancellationToken = default)
        {
            lock (store.gate)
            {
                return ValueTask.FromResult(store.rows.RemoveAll(row => row.Collection == name));
            }
        }

        private List<StoredDocument<T>> Select(Func<Row, bool> filter)
        {
            Row[] found;
            lock (store.gate)
            {
                found = [.. store.rows.Where(row => row.Collection == name && filter(row)).OrderBy(row => row.At).ThenBy(row => row.Sequence)];
            }

            return found
                .Select(row => new StoredDocument<T>(
                    row.Id,
                    DateTimeOffset.FromUnixTimeMilliseconds(row.At),
                    JsonSerializer.Deserialize<T>(row.Content, JsonSerializerOptions.Web)!))
                .ToList();
        }
    }
}
