namespace Kazo.Jarvis.Kernel.Storage;

/// <summary>Identifier of a stored document, given by the store when the document is added.</summary>
/// <param name="Value">A version 7 GUID: ids of documents added later sort after.</param>
public readonly record struct DocumentId(Guid Value)
{
    /// <summary>A new identifier.</summary>
    /// <param name="at">Time of the document, used as the time part of the GUID.</param>
    /// <returns>The identifier.</returns>
    public static DocumentId New(DateTimeOffset at) => new(Guid.CreateVersion7(at));

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
