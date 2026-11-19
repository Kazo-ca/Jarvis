using System.Text.RegularExpressions;

namespace Kazo.Jarvis.Kernel.Storage;

/// <summary>
/// The store of data that grows (ADR 0004, section 3): conversation, then memory and log, by collection.
/// It exposes no SQL. The Windows host uses SQLite (<c>data\jarvis.db</c>), the Web host an in-memory store.
/// </summary>
public interface IDocumentStore
{
    /// <summary>
    /// A collection of documents. A slice only opens its own collections; another slice asks it through a message.
    /// </summary>
    /// <typeparam name="T">The document type, serialized as JSON.</typeparam>
    /// <param name="name">Collection name, <c>&lt;Area&gt;.&lt;Name&gt;</c>, for instance <c>Conversation.Turns</c> (see <see cref="DocumentCollections.IsValidName"/>).</param>
    /// <returns>The collection; opening it again returns an equivalent object.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not valid.</exception>
    IDocumentCollection<T> Collection<T>(string name)
        where T : class;

    /// <summary>
    /// Erases every document of every collection: conversation, memory and log (rule 9). Settings and
    /// secrets are not touched. The erased content does not stay readable in the file.
    /// </summary>
    /// <param name="cancellationToken">Cancels the erasure before it starts.</param>
    /// <returns>A task that completes when everything is erased.</returns>
    ValueTask EraseAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>Rules on collection names.</summary>
public static partial class DocumentCollections
{
    /// <summary>Whether <paramref name="name"/> is a valid collection name: <c>&lt;Area&gt;.&lt;Name&gt;</c>, letters and digits.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidName(string name) => name is not null && NamePattern().IsMatch(name);

    /// <summary>Throws when <paramref name="name"/> is not valid.</summary>
    /// <param name="name">The candidate name.</param>
    /// <exception cref="ArgumentException">The name is not valid.</exception>
    public static void EnsureValidName(string name)
    {
        if (!IsValidName(name))
        {
            throw new ArgumentException($"A document collection is named <Area>.<Name>, not '{name}'.", nameof(name));
        }
    }

    [GeneratedRegex(@"^[A-Z][A-Za-z0-9]{0,49}\.[A-Z][A-Za-z0-9]{0,49}$")]
    private static partial Regex NamePattern();
}
