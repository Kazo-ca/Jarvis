using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Voice.Transcription;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Voice;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Voice;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Features.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>
/// A spoken turn (US1.7) with the real kernel: the listener finds the end of the utterance, the state machine
/// moves to "thinking", Whisper's text is published for the conversation; a cancelled turn publishes nothing.
/// </summary>
[TestFixture]
public sealed class UtteranceListenerTests
{
    private const int Frame = 512;

    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private FakeSpeechRecognition recognition = null!;
    private LoudnessDetection silero = null!;
    private ManualLanguage language = null!;
    private List<IEvent> events = null!;
    private IDisposable subscription = null!;

    private IMessageBus Bus => provider.GetRequiredService<IMessageBus>();

    private IAssistantStateReader State => provider.GetRequiredService<IAssistantStateReader>();

    private IMicrophoneSink Microphone => provider.GetRequiredService<IMicrophoneSink>();

    private UtteranceListener Listener => provider.GetRequiredService<UtteranceListener>();

    private ISettingsStore Settings => provider.GetRequiredService<ISettingsStore>();

    [SetUp]
    public async Task SetUp()
    {
        folder = new TemporaryFolder();
        recognition = new FakeSpeechRecognition();
        silero = new LoudnessDetection();
        language = new ManualLanguage { Current = AssistantLanguage.French };
        provider = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddJarvisKernel()
            .AddSingleton<ISettingsStore>(new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System))
            .AddSingleton<ISpeechRecognition>(recognition)
            .AddSingleton<IVoiceActivityDetection>(silero)
            .AddSingleton<IAssistantLanguageReader>(language)
            .AddTranscriptionSlice()
            .BuildServiceProvider();

        events = [];
        subscription = Bus.Subscribe<IEvent>((message, _) =>
        {
            lock (events)
            {
                events.Add(message);
            }

            return ValueTask.CompletedTask;
        });

        var keeper = provider.GetRequiredService<RecognizerKeeper>();
        await keeper.StartAsync(CancellationToken.None);
        await keeper.WhenLoadedAsync();
    }

    [TearDown]
    public void TearDown()
    {
        subscription.Dispose();
        provider.Dispose();
        folder.Dispose();
    }

    [Test]
    public async Task Silence_after_speech_ends_the_utterance_and_its_text_is_published()
    {
        await Bus.PublishAsync(new ActivationRequested(ActivationSource.Hotkey));
        Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Listening));

        Speak(silence: 5, speech: 40, silenceAfter: 20);

        var transcribed = await WaitForAsync<UtteranceTranscribed>();
        Assert.Multiple(() =>
        {
            Assert.That(transcribed.Text, Is.EqualTo("Quelle heure est-il ?"));
            Assert.That(transcribed.Language, Is.EqualTo(AssistantLanguage.French));
            Assert.That(transcribed.Turn, Is.EqualTo(new TurnId(1)));
            Assert.That(Published<UtteranceEnded>(), Has.Count.EqualTo(1));
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Thinking));
            Assert.That(Listener.LastHeard?.Detector, Is.EqualTo(EndOfSpeechDetector.Silero));
        });

        // From 300 ms before the speech to the end of the 500 ms of silence: 40 speech frames, 16 of silence,
        // and the pre-roll (5 frames were available, 160 ms).
        var (samples, spoken) = recognition.Created[0].Transcribed.Single();
        Assert.Multiple(() =>
        {
            Assert.That(samples, Is.EqualTo((5 + 40 + 16) * Frame));
            Assert.That(spoken, Is.EqualTo(AssistantLanguage.French));
        });
    }

    [Test]
    public async Task Without_speech_the_turn_goes_back_to_idle()
    {
        await Settings.WriteAsync(TranscriptionOptions.Section, new TranscriptionOptions { NoSpeechTimeoutSeconds = 2 });
        await Bus.PublishAsync(new ActivationRequested(ActivationSource.Hotkey));

        Speak(silence: 70, speech: 0, silenceAfter: 0);

        await WaitForAsync<NothingHeard>();
        await WaitUntilAsync(() => State.Current.Phase == AssistantPhase.Idle);
        Assert.Multiple(() =>
        {
            Assert.That(Published<UtteranceEnded>(), Is.Empty);
            Assert.That(recognition.Created[0].Transcribed, Is.Empty);
        });
    }

    [Test]
    public async Task Cancelling_while_listening_publishes_nothing()
    {
        await Bus.PublishAsync(new ActivationRequested(ActivationSource.Hotkey));
        Speak(silence: 0, speech: 20, silenceAfter: 0);

        // A second press cancels the listening (ADR 0005).
        await Bus.PublishAsync(new ActivationRequested(ActivationSource.Hotkey));
        await WaitUntilAsync(() => !Listener.IsBusy);
        Speak(silence: 0, speech: 0, silenceAfter: 30);
        await Task.Delay(100);

        Assert.Multiple(() =>
        {
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Idle));
            Assert.That(Published<UtteranceEnded>(), Is.Empty);
            Assert.That(Published<UtteranceTranscribed>(), Is.Empty);
        });
    }

    [Test]
    public async Task An_interruption_during_the_transcription_drops_its_text()
    {
        recognition.Gate = new TaskCompletionSource();
        await Bus.PublishAsync(new ActivationRequested(ActivationSource.Hotkey));
        Speak(silence: 0, speech: 20, silenceAfter: 20);
        await WaitForAsync<UtteranceEnded>();
        await WaitUntilAsync(() => recognition.Created[0].Transcribed.Count == 1);

        // Hotkey while thinking: the turn is cancelled and a new one listens (US1.12).
        await Bus.PublishAsync(new ActivationRequested(ActivationSource.Hotkey));
        recognition.Gate.SetResult();
        await Task.Delay(100);

        Assert.Multiple(() =>
        {
            Assert.That(Published<UtteranceTranscribed>(), Is.Empty);
            Assert.That(State.Current.Phase, Is.EqualTo(AssistantPhase.Listening));
            Assert.That(State.Current.CurrentTurn, Is.EqualTo(new TurnId(2)));
        });
    }

    [Test]
    public async Task Without_Silero_the_energy_threshold_finds_the_end()
    {
        silero.Missing = true;
        await Bus.PublishAsync(new ActivationRequested(ActivationSource.Hotkey));

        Speak(silence: 10, speech: 40, silenceAfter: 20);

        await WaitForAsync<UtteranceTranscribed>();
        Assert.That(Listener.LastHeard?.Detector, Is.EqualTo(EndOfSpeechDetector.Energy));
    }

    [Test]
    public async Task A_missing_Whisper_model_fails_the_turn_with_the_model_fault()
    {
        recognition.Missing = true;
        var keeper = provider.GetRequiredService<RecognizerKeeper>();
        await Settings.WriteAsync(TranscriptionOptions.Section, new TranscriptionOptions { Model = WhisperModel.Small });
        await WaitUntilAsync(() => keeper.Status.State == RecognizerState.Missing);

        await Bus.PublishAsync(new ActivationRequested(ActivationSource.Hotkey));
        Speak(silence: 0, speech: 20, silenceAfter: 20);

        var failed = await WaitForAsync<TurnFailed>();
        await WaitUntilAsync(() => State.Current.Phase == AssistantPhase.Idle);
        Assert.Multiple(() =>
        {
            Assert.That(failed.Fault.Cause, Is.EqualTo(FaultCause.ModelMissing));
            Assert.That(failed.Fault.Detail, Does.Contain("Small.bin"));
            Assert.That(State.Current.Fault?.Cause, Is.EqualTo(FaultCause.ModelMissing));
        });
    }

    [Test]
    public async Task A_typed_turn_does_not_listen()
    {
        await Bus.PublishAsync(new TextSubmitted("Bonjour"));

        Assert.Multiple(() =>
        {
            Assert.That(Listener.IsBusy, Is.False);
            Assert.That(silero.Created, Is.Zero);
        });
    }

    /// <summary>Writes frames of silence, speech (loud), then silence, as the audio slice would.</summary>
    private void Speak(int silence, int speech, int silenceAfter)
    {
        var quiet = new float[Frame];
        var loud = Enumerable.Range(0, Frame).Select(i => 0.5f * MathF.Sin(2 * MathF.PI * 220 * i / 16_000) + (i % 2 == 0 ? 0.3f : -0.3f)).ToArray();
        for (var i = 0; i < silence; i++)
        {
            Microphone.Write(quiet);
        }

        for (var i = 0; i < speech; i++)
        {
            Microphone.Write(loud);
        }

        for (var i = 0; i < silenceAfter; i++)
        {
            Microphone.Write(quiet);
        }
    }

    private List<T> Published<T>()
    {
        lock (events)
        {
            return [.. events.OfType<T>()];
        }
    }

    private async Task<T> WaitForAsync<T>()
    {
        T? found = default;
        await WaitUntilAsync(() => (found = Published<T>().FirstOrDefault()) is not null);
        return found!;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.That(DateTime.UtcNow, Is.LessThan(deadline), "timed out");
            await Task.Delay(10);
        }
    }
}
