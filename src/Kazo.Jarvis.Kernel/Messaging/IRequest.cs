namespace Kazo.Jarvis.Kernel.Messaging;

/// <summary>
/// A message that expects an answer (ADR 0002, section 5). Exactly one handler answers it.
/// Implementations are immutable records with no behavior, carrying no UI or adapter type.
/// </summary>
/// <typeparam name="TResult">Type of the answer.</typeparam>
public interface IRequest<TResult>;
