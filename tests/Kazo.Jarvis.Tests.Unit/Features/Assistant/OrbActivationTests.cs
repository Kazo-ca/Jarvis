using Kazo.Jarvis.Features.Assistant.Orb;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Tests.Unit.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Tests.Unit.Features.Assistant;

/// <summary>A click on the orb (US1.6) goes through the state machine like the hotkey.</summary>
[TestFixture]
public sealed class OrbActivationTests
{
    private ServiceProvider provider = null!;

    [SetUp]
    public void SetUp() =>
        provider = new ServiceCollection().AddSingleton<ILoggerFactory>(new ListLogger()).AddJarvisKernel().AddOrbSlice().BuildServiceProvider();

    [TearDown]
    public void TearDown() => provider.Dispose();

    [Test]
    public async Task A_click_starts_listening_then_cancels_it()
    {
        var activation = provider.GetRequiredService<OrbActivation>();
        var state = provider.GetRequiredService<IAssistantStateReader>();

        await activation.ClickAsync();
        Assert.That(state.Current.Look, Is.EqualTo(AssistantLook.Listening));

        await activation.ClickAsync();
        Assert.That(state.Current.Look, Is.EqualTo(AssistantLook.Idle));
    }

    [Test]
    public async Task The_activation_comes_from_a_click()
    {
        var bus = provider.GetRequiredService<IMessageBus>();
        ActivationRequested? received = null;
        using var subscription = bus.Subscribe<ActivationRequested>((message, _) =>
        {
            received = message;
            return ValueTask.CompletedTask;
        });

        await provider.GetRequiredService<OrbActivation>().ClickAsync();

        Assert.That(received?.Source, Is.EqualTo(ActivationSource.Click));
    }
}

/// <summary>
/// The right click and the double click on the orb (ADR 0027): the right click asks for the fan at once; the
/// double click asks for the projects view and is never also a single click, so a single click waits for the
/// double-click delay of the host, but only once a slice answers the double click.
/// </summary>
[TestFixture]
public sealed class OrbDoubleClickTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(500);

    private Microsoft.Extensions.Time.Testing.FakeTimeProvider time = null!;
    private ServiceProvider provider = null!;
    private int toggles;
    private ProjectsView projects = null!;
    private IDisposable subscription = null!;

    [SetUp]
    public void SetUp()
    {
        time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        projects = new ProjectsView();
        provider = Build(projects);
        toggles = 0;
        subscription = provider.GetRequiredService<IMessageBus>().Subscribe<Kazo.Jarvis.Kernel.Messages.Desktop.TaskFanToggleRequested>((_, _) =>
        {
            Interlocked.Increment(ref toggles);
            return ValueTask.CompletedTask;
        });
    }

    [TearDown]
    public void TearDown()
    {
        subscription.Dispose();
        provider.Dispose();
    }

    [Test]
    public async Task A_single_click_acts_only_once_the_double_click_delay_has_passed()
    {
        var state = provider.GetRequiredService<IAssistantStateReader>();
        var click = provider.GetRequiredService<OrbActivation>().ClickAsync().AsTask();

        time.Advance(Delay - TimeSpan.FromMilliseconds(1));
        Assert.That(click.IsCompleted, Is.False);
        Assert.That(state.Current.Look, Is.EqualTo(AssistantLook.Idle));

        time.Advance(TimeSpan.FromMilliseconds(1));
        await click.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(state.Current.Look, Is.EqualTo(AssistantLook.Listening));
        Assert.That(toggles, Is.Zero);
    }

    [Test]
    public async Task A_double_click_asks_for_the_projects_view_and_is_never_also_a_single_click()
    {
        var state = provider.GetRequiredService<IAssistantStateReader>();
        var activation = provider.GetRequiredService<OrbActivation>();

        var first = activation.ClickAsync().AsTask();
        time.Advance(TimeSpan.FromMilliseconds(200));
        var second = activation.ClickAsync().AsTask();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        time.Advance(TimeSpan.FromSeconds(2));
        Assert.That(projects.Requests, Is.EqualTo(1));
        Assert.That(toggles, Is.Zero);
        Assert.That(state.Current.Look, Is.EqualTo(AssistantLook.Idle));
    }

    [Test]
    public async Task A_right_click_asks_for_the_fan_at_once_and_never_starts_listening()
    {
        var state = provider.GetRequiredService<IAssistantStateReader>();

        await provider.GetRequiredService<OrbActivation>().RightClickAsync();

        Assert.That(toggles, Is.EqualTo(1));
        Assert.That(projects.Requests, Is.Zero);
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.That(state.Current.Look, Is.EqualTo(AssistantLook.Idle));
    }

    [Test]
    public async Task Without_a_projects_view_a_click_acts_at_once_and_two_clicks_are_two_single_clicks()
    {
        using var bare = Build(null);
        var state = bare.GetRequiredService<IAssistantStateReader>();
        var activation = bare.GetRequiredService<OrbActivation>();

        // No time passes: the delay of the host is not waited for.
        await activation.ClickAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(state.Current.Look, Is.EqualTo(AssistantLook.Listening));

        await activation.ClickAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(state.Current.Look, Is.EqualTo(AssistantLook.Idle));
    }

    [Test]
    public async Task Two_clicks_further_apart_than_the_delay_are_two_single_clicks()
    {
        var state = provider.GetRequiredService<IAssistantStateReader>();
        var activation = provider.GetRequiredService<OrbActivation>();

        var first = activation.ClickAsync().AsTask();
        time.Advance(Delay);
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(state.Current.Look, Is.EqualTo(AssistantLook.Listening));

        var second = activation.ClickAsync().AsTask();
        time.Advance(Delay);
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(state.Current.Look, Is.EqualTo(AssistantLook.Idle));
        Assert.That(toggles, Is.Zero);
    }

    private ServiceProvider Build(ProjectsView? view)
    {
        var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(new ListLogger())
            .AddSingleton<TimeProvider>(time)
            .AddSingleton<Kazo.Jarvis.Kernel.Desktop.IPointerTimings>(new Timings(Delay));
        if (view is not null)
        {
            services.AddSingleton<IEventHandler<Kazo.Jarvis.Kernel.Messages.Desktop.ProjectsViewRequested>>(view);
        }

        return services.AddJarvisKernel().AddOrbSlice().BuildServiceProvider();
    }

    private sealed record Timings(TimeSpan DoubleClickTime) : Kazo.Jarvis.Kernel.Desktop.IPointerTimings;

    /// <summary>Stands in for the slice of the projects view, which does not exist yet.</summary>
    private sealed class ProjectsView : IEventHandler<Kazo.Jarvis.Kernel.Messages.Desktop.ProjectsViewRequested>
    {
        private int requests;

        public int Requests => Volatile.Read(ref requests);

        public ValueTask HandleAsync(Kazo.Jarvis.Kernel.Messages.Desktop.ProjectsViewRequested message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requests);
            return ValueTask.CompletedTask;
        }
    }
}
