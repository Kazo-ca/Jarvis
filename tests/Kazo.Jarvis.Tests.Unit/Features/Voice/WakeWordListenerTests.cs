using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Voice.WakeWord;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Voice;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>
/// The wake word listener (US1.5, ADR 0006, section 2): frames from the microphone stream, threshold from the
/// settings, one activation per detection, silent while listening, models looked for again when missing.
/// </summary>
[TestFixture]
public sealed class WakeWordListenerTests
{
    private const int Frame = 1_280;

    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private ISettingsStore settings = null!;
    private FakeTimeProvider time = null!;
    private FakeWakeWordEngine engine = null!;
    private FakeState state = null!;
    private ListLogger log = null!;
    private WakeWordListener listener = null!;
    private List<ActivationRequested> activations = null!;
    private IDisposable activationSubscription = null!;

    private MicrophoneFeed Feed => provider.GetRequiredService<MicrophoneFeed>();

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        log = new ListLogger();
        provider = new ServiceCollection().AddSingleton<ILoggerFactory>(log).AddJarvisKernel().BuildServiceProvider();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero));
        engine = new FakeWakeWordEngine();
        state = new FakeState();
        activations = [];
        activationSubscription = provider.GetRequiredService<IMessageBus>().Subscribe<ActivationRequested>((message, _) =>
        {
            lock (activations)
            {
                activations.Add(message);
            }

            return ValueTask.CompletedTask;
        });
        listener = CreateListener(state);
    }

    [TearDown]
    public async Task TearDown()
    {
        await listener.StopAsync(CancellationToken.None);
        listener.Dispose();
        activationSubscription.Dispose();
        log.Dispose();
        provider.Dispose();
        folder.Dispose();
    }

    [Test]
    public async Task A_score_over_the_threshold_publishes_one_wake_word_activation()
    {
        engine.Queue([0.1f, 0.6f, 0.9f, 0.8f, 0.2f]);
        await StartAsync();

        Speak(5);

        await Eventually(() => engine.Processed == 5 && listener.Score == 0.2f);
        Assert.Multiple(() =>
        {
            Assert.That(Activations(), Is.EqualTo(new[] { new ActivationRequested(ActivationSource.WakeWord) }));
            Assert.That(listener.LastDetection, Is.EqualTo(new WakeWordDetection(time.GetUtcNow(), 0.6f)));
            Assert.That(listener.Score, Is.EqualTo(0.2f));
            Assert.That(listener.Status.State, Is.EqualTo(WakeWordState.Active));
        });
    }

    [Test]
    public async Task The_score_must_fall_under_the_threshold_before_another_detection_counts()
    {
        engine.Queue([0.9f, 0.95f, 0.1f, 0.7f]);
        await StartAsync();

        Speak(4);

        await Eventually(() => Activations().Count == 2);
        await Task.Delay(50);
        Assert.That(Activations(), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task The_threshold_comes_from_the_settings_and_a_change_applies_at_once()
    {
        await settings.WriteAsync(WakeWordOptions.Section, new WakeWordOptions { Threshold = 0.3f });
        engine.Queue([0.4f, 0.1f]);
        await StartAsync();

        Speak(2);
        await Eventually(() => engine.Processed == 2 && Activations().Count == 1);

        await settings.WriteAsync(WakeWordOptions.Section, new WakeWordOptions { Threshold = 0.8f });
        await Eventually(() => listener.Threshold == 0.8f);
        engine.Queue([0.7f]);
        Speak(1);

        await Eventually(() => engine.Processed == 3 && listener.Score == 0.7f);
        await Task.Delay(50);
        Assert.That(Activations(), Has.Count.EqualTo(1));
    }

    [TestCase(0f, 0.05f)]
    [TestCase(2f, 0.95f)]
    [TestCase(float.NaN, 0.5f)]
    public void A_threshold_out_of_range_is_brought_back(float written, float used) =>
        Assert.That(new WakeWordOptions { Threshold = written }.EffectiveThreshold, Is.EqualTo(used));

    [Test]
    public async Task Nothing_is_scored_while_listening_and_the_detector_starts_afresh_after()
    {
        await StartAsync();
        Speak(1);
        await Eventually(() => engine.Processed == 1);
        var resetsBefore = engine.Resets;

        state.Phase = AssistantPhase.Listening;
        Speak(3);
        await Eventually(() => listener.Status.State == WakeWordState.Paused);
        Assert.That(engine.Processed, Is.EqualTo(1));

        state.Phase = AssistantPhase.Thinking;
        Speak(1);

        await Eventually(() => engine.Processed == 2);
        Assert.Multiple(() =>
        {
            Assert.That(engine.Resets, Is.EqualTo(resetsBefore + 1));
            Assert.That(listener.Status.State, Is.EqualTo(WakeWordState.Active));
        });
    }

    [Test]
    public async Task The_detector_starts_afresh_after_a_detection_even_when_no_pause_was_seen()
    {
        // Listening that begins and ends between two frames: the state the listener reads never leaves Idle.
        engine.Queue([0.9f, 0.1f]);
        await StartAsync();
        Speak(1);
        await Eventually(() => engine.Processed == 1 && Activations().Count == 1);
        var resetsBefore = engine.Resets;

        Speak(1);

        await Eventually(() => engine.Processed == 2);
        Assert.Multiple(() =>
        {
            Assert.That(engine.Resets, Is.EqualTo(resetsBefore + 1));
            Assert.That(listener.Status.State, Is.EqualTo(WakeWordState.Active));
        });
    }

    [Test]
    public async Task Nothing_is_scored_while_the_microphone_is_muted()
    {
        await StartAsync();
        state.Muted = true;

        Speak(2);

        await Eventually(() => listener.Status.State == WakeWordState.Paused && Feed.Written == 2 * Frame);
        await Task.Delay(50);
        Assert.That(engine.Processed, Is.Zero);
    }

    [Test]
    public async Task Blocks_of_any_size_are_cut_into_frames()
    {
        await StartAsync();

        // 50 ms blocks, as the Windows capture delivers them: 800 samples, not a multiple of a frame.
        for (var i = 0; i < 9; i++)
        {
            Feed.Write(new float[800]);
        }

        await Eventually(() => engine.Processed == 9 * 800 / Frame);
        Assert.That(engine.FrameLengths, Is.All.EqualTo(Frame));
    }

    [Test]
    public async Task Disposing_twice_does_nothing_the_second_time()
    {
        await StartAsync();
        await listener.StopAsync(CancellationToken.None);

        listener.Dispose();

        Assert.DoesNotThrow(listener.Dispose);
    }

    [Test]
    public async Task Missing_models_are_named_and_looked_for_again()
    {
        engine.Missing = ["/models/wake-word/hey_jarvis_v0.1.onnx"];
        await listener.StartAsync(CancellationToken.None);

        await Eventually(() => listener.Status.State == WakeWordState.ModelsMissing);
        Assert.That(listener.Status.MissingFiles, Is.EqualTo(engine.Missing));
        Assert.That(log.Entries, Has.Some.Matches<(LogLevel Level, string Message, Exception? Exception)>(entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("hey_jarvis_v0.1.onnx", StringComparison.Ordinal)));

        engine.Missing = null;
        await Eventually(() =>
        {
            time.Advance(TimeSpan.FromSeconds(30));
            return listener.Status.State == WakeWordState.Paused;
        });

        Speak(1);
        await Eventually(() => engine.Processed == 1);
        Assert.That(log.Entries.Count(entry => entry.Level == LogLevel.Warning), Is.EqualTo(1));
    }

    [Test]
    public async Task With_the_real_state_machine_a_detection_starts_listening_and_silences_the_wake_word()
    {
        await listener.StopAsync(CancellationToken.None);
        listener.Dispose();
        listener = CreateListener(provider.GetRequiredService<IAssistantStateReader>());
        var reader = provider.GetRequiredService<IAssistantStateReader>();
        engine.Queue([0.9f, 0.9f, 0.9f]);
        await StartAsync();

        Speak(1);
        await Eventually(() => reader.Current.Phase == AssistantPhase.Listening);
        Speak(2);

        await Eventually(() => listener.Status.State == WakeWordState.Paused);
        Assert.Multiple(() =>
        {
            Assert.That(engine.Processed, Is.EqualTo(1));
            Assert.That(Activations(), Has.Count.EqualTo(1));
        });
    }

    private WakeWordListener CreateListener(IAssistantStateReader reader) => new(
        engine,
        Feed,
        reader,
        provider.GetRequiredService<IMessageBus>(),
        settings,
        time,
        log.CreateLogger<WakeWordListener>());

    private async Task StartAsync()
    {
        await listener.StartAsync(CancellationToken.None);

        // The models load on the listener's own task; frames written before that are not heard.
        await Eventually(() => listener.Status.State == WakeWordState.Paused);
    }

    private void Speak(int frames) => Feed.Write(new float[frames * Frame]);

    private List<ActivationRequested> Activations()
    {
        lock (activations)
        {
            return [.. activations];
        }
    }

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The condition did not become true within 5 seconds.");
            }

            await Task.Delay(10);
        }
    }

    /// <summary>An assistant state set by the test.</summary>
    private sealed class FakeState : IAssistantStateReader
    {
        public volatile bool Muted;

        public AssistantPhase Phase { get; set; } = AssistantPhase.Idle;

        public AssistantState Current => AssistantState.Initial with { Phase = Phase, MicrophoneMuted = Muted };
    }

    /// <summary>A wake word engine whose detector returns the scores queued by the test, then 0.</summary>
    private sealed class FakeWakeWordEngine : IWakeWordEngine
    {
        private int processed;
        private int resets;

        public Queue<float[]> Scores { get; } = new();

        public void Queue(float[] scores)
        {
            lock (Scores)
            {
                Scores.Enqueue(scores);
            }
        }

        public string[]? Missing { get; set; }

        public int Processed => Volatile.Read(ref processed);

        public int Resets => Volatile.Read(ref resets);

        public List<int> FrameLengths { get; } = [];

        public IWakeWordDetector Load() =>
            Missing is { } missing ? throw new WakeWordModelMissingException(missing) : new Detector(this);

        private sealed class Detector(FakeWakeWordEngine owner) : IWakeWordDetector
        {
            private readonly Queue<float> next = new();

            public int FrameLength => Frame;

            public float Process(ReadOnlySpan<float> frame)
            {
                lock (owner.FrameLengths)
                {
                    owner.FrameLengths.Add(frame.Length);
                }

                lock (owner.Scores)
                {
                    while (owner.Scores.TryDequeue(out var scores))
                    {
                        foreach (var value in scores)
                        {
                            next.Enqueue(value);
                        }
                    }
                }

                Interlocked.Increment(ref owner.processed);
                return next.TryDequeue(out var score) ? score : 0f;
            }

            public void Reset() => Interlocked.Increment(ref owner.resets);

            public void Dispose()
            {
            }
        }
    }
}
