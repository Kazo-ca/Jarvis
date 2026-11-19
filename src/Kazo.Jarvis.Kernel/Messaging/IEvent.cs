namespace Kazo.Jarvis.Kernel.Messaging;

/// <summary>
/// A past fact, published on the bus (ADR 0002, section 5). Zero, one or several subscribers receive it.
/// Implementations are immutable records with no behavior, carrying no UI or adapter type.
/// </summary>
public interface IEvent;
