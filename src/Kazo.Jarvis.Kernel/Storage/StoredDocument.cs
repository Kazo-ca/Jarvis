namespace Kazo.Jarvis.Kernel.Storage;

/// <summary>A document as the store keeps it.</summary>
/// <typeparam name="T">The document type, owned by the slice that owns the collection.</typeparam>
/// <param name="Id">Its identifier.</param>
/// <param name="At">The time it belongs to (a conversation turn, a log entry). Periods are read on it.</param>
/// <param name="Content">The document.</param>
public sealed record StoredDocument<T>(DocumentId Id, DateTimeOffset At, T Content);
