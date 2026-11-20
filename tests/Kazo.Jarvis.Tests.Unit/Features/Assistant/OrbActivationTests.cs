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
