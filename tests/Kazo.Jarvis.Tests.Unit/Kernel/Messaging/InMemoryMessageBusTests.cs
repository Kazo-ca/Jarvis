using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Messaging;

/// <summary>The in-memory bus of ADR 0002, section 5.</summary>
[TestFixture]
public sealed class InMemoryMessageBusTests
{
    private ListLogger log = null!;
    private Journal journal = null!;

    [SetUp]
    public void SetUp()
    {
        log = new ListLogger();
        journal = new Journal();
    }

    [TearDown]
    public void TearDown() => log.Dispose();

    [Test]
    public async Task An_event_reaches_the_registered_handlers_then_the_subscriptions_in_order()
    {
        using var provider = Build(services => services
            .AddSingleton<IEventHandler<Pinged>>(new RecordingHandler("first", journal))
            .AddSingleton<IEventHandler<Pinged>>(new RecordingHandler("second", journal)));
        var bus = provider.GetRequiredService<IMessageBus>();
        using var subscription = bus.Subscribe<Pinged>((message, _) => journal.Add($"subscription {message.Text}"));

        await bus.PublishAsync(new Pinged("a"));

        Assert.That(journal.Lines, Is.EqualTo(new[] { "first a", "second a", "subscription a" }));
    }

    [Test]
    public async Task An_event_with_no_subscriber_is_fine()
    {
        using var provider = Build();

        await provider.GetRequiredService<IMessageBus>().PublishAsync(new Pinged("nobody"));

        Assert.That(log.Entries, Is.Empty);
    }

    [Test]
    public async Task A_subscriber_of_a_base_type_receives_the_derived_events()
    {
        using var provider = Build();
        var bus = provider.GetRequiredService<IMessageBus>();
        using var all = bus.Subscribe<IEvent>((message, _) => journal.Add($"event {message.GetType().Name}"));
        using var base_ = bus.Subscribe<Animal>((message, _) => journal.Add($"animal {message.Name}"));
        using var exact = bus.Subscribe<Dog>((message, _) => journal.Add($"dog {message.Name}"));

        await bus.PublishAsync<Animal>(new Dog("rex"));
        await bus.PublishAsync(new Pinged("x"));

        Assert.That(journal.Lines, Is.EqualTo(new[] { "event Dog", "animal rex", "dog rex", "event Pinged" }));
    }

    [Test]
    public async Task A_handler_registered_for_a_base_type_receives_the_derived_events()
    {
        using var provider = Build(services => services.AddSingleton<IEventHandler<Animal>>(new AnimalHandler(journal)));

        await provider.GetRequiredService<IMessageBus>().PublishAsync(new Dog("rex"));

        Assert.That(journal.Lines, Is.EqualTo(new[] { "handler rex" }));
    }

    [Test]
    public async Task A_failing_subscriber_does_not_stop_the_others_and_the_error_is_logged()
    {
        using var provider = Build(services => services
            .AddSingleton<IEventHandler<Pinged>>(new ThrowingHandler())
            .AddSingleton<IEventHandler<Pinged>>(new RecordingHandler("after the throwing handler", journal)));
        var bus = provider.GetRequiredService<IMessageBus>();
        using var failing = bus.Subscribe<Pinged>(async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("asynchronous failure");
        });
        using var last = bus.Subscribe<Pinged>((message, _) => journal.Add($"last {message.Text}"));

        await bus.PublishAsync(new Pinged("a"));

        Assert.That(journal.Lines, Is.EqualTo(new[] { "after the throwing handler a", "last a" }));
        var errors = log.Entries.Where(entry => entry.Level == LogLevel.Error).ToArray();
        Assert.That(errors, Has.Length.EqualTo(2));
        Assert.That(errors.Select(entry => entry.Exception?.Message), Is.EquivalentTo(new[] { "synchronous failure", "asynchronous failure" }));
    }

    [Test]
    public async Task A_disposed_subscription_no_longer_receives_events()
    {
        using var provider = Build();
        var bus = provider.GetRequiredService<IMessageBus>();
        var subscription = bus.Subscribe<Pinged>((message, _) => journal.Add(message.Text));

        await bus.PublishAsync(new Pinged("before"));
        subscription.Dispose();
        subscription.Dispose();
        await bus.PublishAsync(new Pinged("after"));

        Assert.That(journal.Lines, Is.EqualTo(new[] { "before" }));
    }

    [Test]
    public void A_cancelled_publication_stops_and_throws()
    {
        using var provider = Build();
        var bus = provider.GetRequiredService<IMessageBus>();
        using var cancellation = new CancellationTokenSource();
        using var first = bus.Subscribe<Pinged>((_, _) =>
        {
            cancellation.Cancel();
            return ValueTask.CompletedTask;
        });
        using var second = bus.Subscribe<Pinged>((message, _) => journal.Add(message.Text));

        Assert.That(async () => await bus.PublishAsync(new Pinged("a"), cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(journal.Lines, Is.Empty);
    }

    [Test]
    public async Task A_request_is_answered_by_its_single_handler()
    {
        using var provider = Build(services => services.AddSingleton<IRequestHandler<Add, int>, AddHandler>());

        var sum = await provider.GetRequiredService<IMessageBus>().SendAsync(new Add(2, 3));

        Assert.That(sum, Is.EqualTo(5));
    }

    [Test]
    public void A_request_without_a_handler_throws()
    {
        using var provider = Build();

        Assert.That(
            async () => await provider.GetRequiredService<IMessageBus>().SendAsync(new Add(1, 1)),
            Throws.InvalidOperationException.With.Message.Contains("0 registered for Add"));
    }

    [Test]
    public void A_request_with_two_handlers_throws()
    {
        using var provider = Build(services => services
            .AddSingleton<IRequestHandler<Add, int>, AddHandler>()
            .AddSingleton<IRequestHandler<Add, int>, AddHandler>());

        Assert.That(
            async () => await provider.GetRequiredService<IMessageBus>().SendAsync(new Add(1, 1)),
            Throws.InvalidOperationException.With.Message.Contains("2 registered for Add"));
    }

    [Test]
    public void The_exception_of_a_request_handler_reaches_the_caller()
    {
        using var provider = Build(services => services.AddSingleton<IRequestHandler<Add, int>, OverflowHandler>());

        Assert.That(
            async () => await provider.GetRequiredService<IMessageBus>().SendAsync(new Add(int.MaxValue, 1)),
            Throws.InstanceOf<OverflowException>());
    }

    [Test]
    public async Task The_bus_works_in_a_host_without_logging()
    {
        using var provider = new ServiceCollection().AddJarvisKernel().BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();
        using var failing = bus.Subscribe<Pinged>((_, _) => throw new InvalidOperationException("nobody logs this"));

        Assert.That(async () => await bus.PublishAsync(new Pinged("a")), Throws.Nothing);
        await Task.CompletedTask;
    }

    private ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(log);
        services.AddJarvisKernel();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private sealed record Pinged(string Text) : IEvent;

    private abstract record Animal(string Name) : IEvent;

    private sealed record Dog(string Name) : Animal(Name);

    private sealed record Add(int Left, int Right) : IRequest<int>;

    private sealed class Journal
    {
        public List<string> Lines { get; } = [];

        public ValueTask Add(string line)
        {
            Lines.Add(line);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingHandler(string name, Journal journal) : IEventHandler<Pinged>
    {
        public ValueTask HandleAsync(Pinged message, CancellationToken cancellationToken) => journal.Add($"{name} {message.Text}");
    }

    private sealed class AnimalHandler(Journal journal) : IEventHandler<Animal>
    {
        public ValueTask HandleAsync(Animal message, CancellationToken cancellationToken) => journal.Add($"handler {message.Name}");
    }

    private sealed class ThrowingHandler : IEventHandler<Pinged>
    {
        public ValueTask HandleAsync(Pinged message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("synchronous failure");
    }

    private sealed class AddHandler : IRequestHandler<Add, int>
    {
        public ValueTask<int> HandleAsync(Add request, CancellationToken cancellationToken) => ValueTask.FromResult(request.Left + request.Right);
    }

    private sealed class OverflowHandler : IRequestHandler<Add, int>
    {
        public ValueTask<int> HandleAsync(Add request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(checked(request.Left + request.Right));
    }
}
