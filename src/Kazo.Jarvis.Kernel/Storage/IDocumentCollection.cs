namespace Kazo.Jarvis.Kernel.Storage;

/// <summary>The documents of one collection (ADR 0004, section 3).</summary>
/// <typeparam name="T">The document type, serialized as JSON.</typeparam>
public interface IDocumentCollection<T>
    where T : class
{
    /// <summary>The collection name.</summary>
    string Name { get; }

    /// <summary>Adds a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="at">The time it belongs to; now when omitted.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The stored document, with its new identifier.</returns>
    ValueTask<StoredDocument<T>> AddAsync(T document, DateTimeOffset? at = null, CancellationToken cancellationToken = default);

    /// <summary>Reads one document.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The document, or <see langword="null"/> when it is not in this collection.</returns>
    ValueTask<StoredDocument<T>?> GetAsync(DocumentId id, CancellationToken cancellationToken = default);

    /// <summary>Reads the documents of a period, oldest first; documents with the same time come in the order they were added.</summary>
    /// <param name="from">Start of the period, included.</param>
    /// <param name="to">End of the period, excluded.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The documents.</returns>
    ValueTask<IReadOnlyList<StoredDocument<T>>> ReadPeriodAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>Deletes one document.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when it was in this collection.</returns>
    ValueTask<bool> DeleteAsync(DocumentId id, CancellationToken cancellationToken = default);

    /// <summary>Deletes every document of this collection.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of documents deleted.</returns>
    ValueTask<int> ClearAsync(CancellationToken cancellationToken = default);
}
