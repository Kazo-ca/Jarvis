using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Assistant;

/// <summary>The single writer of the state, wired to the bus (ADR 0005, section 4).</summary>
[TestFixture]
public sealed class AssistantStateKeeperTests
{
    private ListLogger log = null!;
    private ServiceProvider provider = null!;
    private IMessageBus bus = null!;
    private List<IEvent> published = null!;
    private IDisposable recording = null!;

    [SetUp]
    public void SetUp()
    {
        log = new ListLogger();
        provider = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(log)
            .AddJarvisKernel()
            .BuildServiceProvider();
        bus = provider.GetRequiredService<IMessageBus>();
        published = [];
        recording = bus.Subscribe<IEvent>((message, _) =>
        {
            lock (published)
            {
                published.Add(message);
            }

            return ValueTask.CompletedTask;
        });
    }

    [TearDown]
    public void TearDown()
    {
        recording.Dispose();
        provider.Dispose();
        log.Dispose();
    }

    [Test]
    public async Task A_published_input_changes_the_state_and_publishes_the_change_then_the_effects()
    {
        await bus.PublishAsync(new ActivationRequested(ActivationSource.Hotkey));

        var reader = provider.GetRequiredService<IAssistantStateReader>();
        Assert.That(reader.Current.Phase, Is.EqualTo(AssistantPhase.Listening));
        var outputs = published.Where(message => message is not AssistantInput).ToArray();
        Assert.That(outputs, Has.Length.EqualTo(2));
        Assert.That(outputs[0], Is.EqualTo(new AssistantStateChanged(AssistantState.Initial, reader.Current)));
        Assert.That(((AssistantStateChanged)outputs[0]).Look, Is.EqualTo(AssistantLook.Listening));
        Assert.That(outputs[1], Is.EqualTo(new TurnStarted(new TurnId(1), TurnInput.Voice, null)));
    }

    [Test]
    public async Task A_state_change_is_logged_as_information()
    {
        await bus.PublishAsync(new ActivationRequested(ActivationSource.Hotkey));

        Assert.That(log.Entries.Any(entry => entry.Level == LogLevel.Information
            && entry.Message.Contains("ActivationRequested: Idle -> Listening")), Is.True);
    }

    [Test]
    public async Task An_ignored_input_publishes_nothing_and_is_logged()
    {
        await bus.PublishAsync(new SpeechEnded(new TurnId(7)));

        Assert.That(published.Where(message => message is not AssistantInput), Is.Empty);
        Assert.That(log.Entries.Any(entry => entry.Level == LogLevel.Debug && entry.Message.Contains("SpeechEnded ignored")), Is.True);
    }

    [Test]
    public async Task A_refused_activation_publishes_the_refusal_without_a_state_change()
    {
        await bus.PublishAsync(new MicrophoneMuteChanged(true));
        published.Clear();

        await bus.PublishAsync(new ActivationRequested(ActivationSource.WakeWord));

        Assert.That(
            published.Where(message => message is not AssistantInput),
            Is.EqualTo(new IEvent[] { new ActivationRefused(ActivationSource.WakeWord, ActivationRefusal.MicrophoneMuted) }));
    }

    [Test]
    public async Task An_input_published_by_a_subscriber_is_processed_after_the_current_one()
    {
        // A slice that reacts to the new turn by ending the utterance at once.
        using var reacting = bus.Subscribe<TurnStarted>((started, token) => bus.PublishAsync(new UtteranceEnded(started.Turn), token));

        await bus.PublishAsync(new ActivationRequested(ActivationSource.Click));

        var changes = published.OfType<AssistantStateChanged>().Select(change => change.Current.Phase).ToArray();
        Assert.That(changes, Is.EqualTo(new[] { AssistantPhase.Listening, AssistantPhase.Thinking }));
        var order = published.Where(message => message is AssistantStateChanged or TurnStarted).Select(message => message.GetType().Name);
        Assert.That(order, Is.EqualTo(new[] { "AssistantStateChanged", "TurnStarted", "AssistantStateChanged" }),
            "the second change comes after the effects of the first one");
    }

    [Test]
    public async Task A_full_spoken_turn_with_an_interruption()
    {
        await bus.PublishAsync(new ActivationRequested(ActivationSource.WakeWord));
        await bus.PublishAsync(new UtteranceEnded(new TurnId(1)));
        await bus.PublishAsync(new FirstSentenceReady(new TurnId(1)));
        await bus.PublishAsync(new ActivationRequested(ActivationSource.WakeWord));
        await bus.PublishAsync(new SpeechEnded(new TurnId(1)));
        await bus.PublishAsync(new UtteranceEnded(new TurnId(2)));
        await bus.PublishAsync(new AnswerFinishedWithoutSpeech(new TurnId(2)));

        var looks = published.OfType<AssistantStateChanged>().Select(change => change.Look);
        Assert.That(looks, Is.EqualTo(new[]
        {
            AssistantLook.Listening, AssistantLook.Thinking, AssistantLook.Speaking,
            AssistantLook.Listening, AssistantLook.Thinking, AssistantLook.Idle,
        }));
        Assert.That(published.OfType<TurnCancelled>(), Is.EqualTo(new[] { new TurnCancelled(new TurnId(1), TurnCancelReason.Interrupted) }));
    }

    [Test]
    public async Task Concurrent_inputs_are_processed_one_at_a_time()
    {
        // 200 tasks change the number of delegated tasks at the same time: each change is applied alone,
        // so every published change starts from the state the previous one left.
        await Task.WhenAll(Enumerable.Range(1, 200).Select(count =>
            Task.Run(async () => await bus.PublishAsync(new DelegatedTaskCountChanged(count)))));

        var changes = published.OfType<AssistantStateChanged>().ToArray();
        Assert.That(changes, Has.Length.EqualTo(200));
        for (var index = 1; index < changes.Length; index++)
        {
            Assert.That(changes[index].Previous, Is.EqualTo(changes[index - 1].Current));
        }

        Assert.That(provider.GetRequiredService<IAssistantStateReader>().Current, Is.EqualTo(changes[^1].Current));
    }
}
