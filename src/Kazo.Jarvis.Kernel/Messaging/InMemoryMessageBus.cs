using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Kernel.Messaging;

/// <summary>
/// The kernel's bus (ADR 0002, section 5). Delivers events in order, one subscriber after the other:
/// first the handlers registered in the container, then the subscriptions, in the order they were made.
/// </summary>
public sealed partial class InMemoryMessageBus : IMessageBus
{
    private static readonly ConcurrentDictionary<Type, Type[]> DeliveryTypes = new();
    private static readonly ConcurrentDictionary<Type, EventDispatcher> EventDispatchers = new();
    private static readonly ConcurrentDictionary<Type, object> RequestDispatchers = new();

    private readonly IServiceProvider services;
    private readonly ILogger logger;
    private readonly Lock gate = new();
    private readonly List<Subscription> subscriptions = [];

    /// <summary>Creates the bus.</summary>
    /// <param name="services">
    /// Root provider, used to resolve <see cref="IEventHandler{TEvent}"/> and <see cref="IRequestHandler{TRequest, TResult}"/>.
    /// Its <see cref="ILoggerFactory"/> is used when there is one; a host without logging still gets a working bus.
    /// </param>
    public InMemoryMessageBus(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        this.services = services;
        logger = (services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance).CreateLogger<InMemoryMessageBus>();
    }

    /// <inheritdoc />
    public async ValueTask PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(message);

        foreach (var deliveryType in DeliveryTypesOf(message.GetType()))
        {
            var dispatcher = EventDispatchers.GetOrAdd(deliveryType, static type =>
                (EventDispatcher)Activator.CreateInstance(typeof(EventDispatcher<>).MakeGenericType(type))!);
            foreach (var (name, deliver) in dispatcher.Handlers(services, message))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DeliverAsync(message, name, () => deliver(cancellationToken), cancellationToken);
            }
        }

        foreach (var subscription in SubscriptionsFor(message.GetType()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!subscription.IsDisposed)
            {
                await DeliverAsync(message, subscription.Name, () => subscription.Invoke(message, cancellationToken), cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dispatcher = (RequestDispatcher<TResult>)RequestDispatchers.GetOrAdd(request.GetType(), static type =>
            Activator.CreateInstance(typeof(RequestDispatcher<,>).MakeGenericType(type, typeof(TResult)))!);
        return dispatcher.SendAsync(services, request, cancellationToken);
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, ValueTask> handler)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new Subscription(
            typeof(TEvent),
            handler.Method.DeclaringType?.Name ?? typeof(TEvent).Name,
            (message, token) => handler((TEvent)message, token),
            Remove);
        lock (gate)
        {
            subscriptions.Add(subscription);
        }

        return subscription;
    }

    /// <summary>The runtime type, then its base types, then its interfaces that are events.</summary>
    private static Type[] DeliveryTypesOf(Type eventType) => DeliveryTypes.GetOrAdd(eventType, static type =>
    {
        var types = new List<Type>();
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            types.Add(current);
        }

        types.AddRange(type.GetInterfaces().Where(typeof(IEvent).IsAssignableFrom));
        return [.. types];
    });

    private Subscription[] SubscriptionsFor(Type eventType)
    {
        lock (gate)
        {
            return [.. subscriptions.Where(subscription => subscription.EventType.IsAssignableFrom(eventType))];
        }
    }

    private void Remove(Subscription subscription)
    {
        lock (gate)
        {
            subscriptions.Remove(subscription);
        }
    }

    private async ValueTask DeliverAsync(IEvent message, string subscriber, Func<ValueTask> deliver, CancellationToken cancellationToken)
    {
        try
        {
            await deliver();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A failing subscriber does not stop the others (ADR 0002, section 5).
            LogSubscriberFailed(exception, subscriber, message.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Subscriber {Subscriber} failed on {Event}; the other subscribers still receive it.")]
    private partial void LogSubscriberFailed(Exception exception, string subscriber, string @event);

    /// <summary>Resolves the handlers of one delivery type, typed, without reflection on each call.</summary>
    private abstract class EventDispatcher
    {
        public abstract IEnumerable<(string Name, Func<CancellationToken, ValueTask> Deliver)> Handlers(IServiceProvider services, IEvent message);
    }

    private sealed class EventDispatcher<TEvent> : EventDispatcher
        where TEvent : IEvent
    {
        public override IEnumerable<(string Name, Func<CancellationToken, ValueTask> Deliver)> Handlers(IServiceProvider services, IEvent message) =>
            services.GetServices<IEventHandler<TEvent>>()
                .Select(handler => (handler.GetType().Name, (Func<CancellationToken, ValueTask>)(token => handler.HandleAsync((TEvent)message, token))));
    }

    private abstract class RequestDispatcher<TResult>
    {
        public abstract ValueTask<TResult> SendAsync(IServiceProvider services, IRequest<TResult> request, CancellationToken cancellationToken);
    }

    private sealed class RequestDispatcher<TRequest, TResult> : RequestDispatcher<TResult>
        where TRequest : IRequest<TResult>
    {
        public override ValueTask<TResult> SendAsync(IServiceProvider services, IRequest<TResult> request, CancellationToken cancellationToken)
        {
            var handlers = services.GetServices<IRequestHandler<TRequest, TResult>>().ToArray();
            if (handlers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"A request has exactly one handler: {handlers.Length} registered for {typeof(TRequest).Name}.");
            }

            return handlers[0].HandleAsync((TRequest)request, cancellationToken);
        }
    }

    private sealed class Subscription(
        Type eventType,
        string name,
        Func<IEvent, CancellationToken, ValueTask> invoke,
        Action<Subscription> remove) : IDisposable
    {
        private int disposed;

        public Type EventType { get; } = eventType;

        public string Name { get; } = name;

        public bool IsDisposed => Volatile.Read(ref disposed) == 1;

        public ValueTask Invoke(IEvent message, CancellationToken cancellationToken) => invoke(message, cancellationToken);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                remove(this);
            }
        }
    }
}
