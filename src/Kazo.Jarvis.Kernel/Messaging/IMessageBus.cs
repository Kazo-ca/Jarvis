namespace Kazo.Jarvis.Kernel.Messaging;

/// <summary>
/// In-memory bus between slices (ADR 0002, section 5): typed, asynchronous, no external mediator.
/// High-rate streams (audio samples, sound level) never go through it.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Delivers <paramref name="message"/> to every subscriber of its runtime type and of each of its base
    /// types and interfaces, one after the other. A failing subscriber is logged and does not stop the others.
    /// </summary>
    /// <typeparam name="TEvent">Static type of the event.</typeparam>
    /// <param name="message">The event.</param>
    /// <param name="cancellationToken">Cancels the delivery; subscribers not reached yet do not receive it.</param>
    /// <returns>A task that completes when every subscriber has handled the event.</returns>
    ValueTask PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    /// <summary>Sends <paramref name="request"/> to its single handler and returns the answer.</summary>
    /// <typeparam name="TResult">Type of the answer.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the handling.</param>
    /// <returns>The answer of the handler. Its exceptions reach the caller.</returns>
    /// <exception cref="InvalidOperationException">No handler, or more than one, is registered for the request type.</exception>
    ValueTask<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes a handler for the life of a component or a service (a Razor component, for instance).
    /// Handlers known at start-up are better registered as <see cref="IEventHandler{TEvent}"/>.
    /// </summary>
    /// <typeparam name="TEvent">Type of the events received, base types and interfaces included.</typeparam>
    /// <param name="handler">The handler.</param>
    /// <returns>Disposing it ends the subscription.</returns>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, ValueTask> handler)
        where TEvent : IEvent;
}
