namespace Kazo.Jarvis.Kernel.Messaging;

/// <summary>
/// A subscriber registered in the service container. The bus resolves every handler of the event type,
/// and of each base type and interface of the event, as singletons from the root provider.
/// </summary>
/// <typeparam name="TEvent">Type of the event received.</typeparam>
public interface IEventHandler<in TEvent>
    where TEvent : IEvent
{
    /// <summary>Handles one event. An exception is logged by the bus and does not reach the other subscribers.</summary>
    /// <param name="message">The event.</param>
    /// <param name="cancellationToken">Cancels the handling.</param>
    /// <returns>A task that completes when the event is handled.</returns>
    ValueTask HandleAsync(TEvent message, CancellationToken cancellationToken);
}
