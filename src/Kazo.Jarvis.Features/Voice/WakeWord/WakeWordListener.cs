using System.Threading.Channels;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Voice;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Voice.WakeWord;

/// <summary>
/// Background service of the wake word (US1.5, ADR 0006, section 2): scores the microphone stream frame by
/// frame and publishes <see cref="ActivationRequested"/> with <see cref="ActivationSource.WakeWord"/> when the
/// score reaches the threshold.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>One detection per activation: after a detection, the score must fall below the threshold before
/// another one counts, so a long "hey jarviiis" does not publish twice.</item>
/// <item>Silent while Jarvis listens (and while the microphone is muted): no frame is scored, and the detector
/// forgets what it heard before, so the end of the request cannot wake it again. It keeps scoring while Jarvis
/// thinks or speaks: "hey jarvis" then interrupts (US1.12).</item>
/// <item>Models missing: the hotkey and the click still work (US1.6), so this is not a fault of the
/// assistant; the settings page says so and the models are looked for again every
/// <see cref="RetryInterval"/> (the first-run wizard downloads them, work item 14).</item>
/// </list>
/// The capture thread only copies the samples into a queue; the models run on this service's own task.
/// </remarks>
public sealed partial class WakeWordListener : IHostedService, IDisposable
{
    /// <summary>How often missing or broken models are looked for again.</summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);

    // About 6 s of 50 ms blocks. If the models fall that far behind, old audio is dropped: a late wake word
    // is worse than a missed one.
    private const int QueueCapacity = 120;

    private readonly IWakeWordEngine engine;
    private readonly IMicrophoneSource microphone;
    private readonly IAssistantStateReader state;
    private readonly IMessageBus bus;
    private readonly ISettingsStore settings;
    private readonly TimeProvider time;
    private readonly ILogger<WakeWordListener> logger;
    private readonly Channel<float[]> queue = Channel.CreateBounded<float[]>(
        new BoundedChannelOptions(QueueCapacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private CancellationTokenSource? stopping;
    private Task? running;
    private IDisposable? settingsWatch;
    private float threshold = WakeWordOptions.DefaultThreshold;
    private WakeWordStatus status = WakeWordStatus.Starting;
    private WakeWordDetection? lastDetection;
    private float score;

    /// <summary>Creates the listener.</summary>
    /// <param name="engine">The host's wake word engine.</param>
    /// <param name="microphone">The microphone stream of the kernel, filled by the audio slice.</param>
    /// <param name="state">The assistant state: silent while listening or muted.</param>
    /// <param name="bus">Where the activation goes.</param>
    /// <param name="settings">The settings, for <see cref="WakeWordOptions.Section"/>.</param>
    /// <param name="time">The clock of the retries and of the last detection.</param>
    /// <param name="logger">Logs the model problems and the detections.</param>
    public WakeWordListener(
        IWakeWordEngine engine,
        IMicrophoneSource microphone,
        IAssistantStateReader state,
        IMessageBus bus,
        ISettingsStore settings,
        TimeProvider time,
        ILogger<WakeWordListener> logger)
    {
        this.engine = engine;
        this.microphone = microphone;
        this.state = state;
        this.bus = bus;
        this.settings = settings;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>Raised after each change of <see cref="Status"/> or <see cref="LastDetection"/>, on any thread.</summary>
    public event Action? StatusChanged;

    /// <summary>Raised with each new score (every 80 ms while active), on the listener's thread.</summary>
    public event Action<float>? ScoreReported;

    /// <summary>What the wake word does now.</summary>
    public WakeWordStatus Status => Volatile.Read(ref status);

    /// <summary>The last detection since the start, or <see langword="null"/>.</summary>
    public WakeWordDetection? LastDetection => Volatile.Read(ref lastDetection);

    /// <summary>The latest score, from 0 to 1; 0 while not active.</summary>
    public float Score => Volatile.Read(ref score);

    /// <summary>The threshold in use.</summary>
    public float Threshold => Volatile.Read(ref threshold);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Volatile.Write(ref threshold, settings.Read(WakeWordOptions.Section).EffectiveThreshold);
        settingsWatch = settings.Watch(WakeWordOptions.Section, changed => Volatile.Write(ref threshold, changed.EffectiveThreshold));
        stopping = new CancellationTokenSource();
        var token = stopping.Token;

        // Loading the models takes a few hundred milliseconds: never on the start-up path.
        running = Task.Run(() => RunAsync(token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        settingsWatch?.Dispose();
        settingsWatch = null;
        if (stopping is null || running is null)
        {
            return;
        }

        await stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await running.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopped while loading or waiting: nothing else to do.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        settingsWatch?.Dispose();
        stopping?.Cancel();
        stopping?.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            using var detector = await LoadAsync(token).ConfigureAwait(false);
            using var subscription = microphone.Subscribe(samples => queue.Writer.TryWrite(samples.ToArray()));
            await ListenAsync(detector, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal stop.
        }
        catch (Exception exception)
        {
            // A detector that fails while running is not tried again until the next start: it would fail
            // every 80 ms. The hotkey and the click still work.
            LogRunFailed(exception);
            SetStatus(WakeWordStatus.Failed);
        }
    }

    /// <summary>Loads the models, trying again every <see cref="RetryInterval"/> until they are there.</summary>
    private async Task<IWakeWordDetector> LoadAsync(CancellationToken token)
    {
        var logged = false;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var detector = engine.Load();
                if (logged)
                {
                    LogLoadedAfterAll();
                }

                SetStatus(WakeWordStatus.Paused);
                return detector;
            }
            catch (WakeWordModelMissingException exception)
            {
                if (!logged)
                {
                    LogModelsMissing(string.Join(", ", exception.MissingFiles));
                }

                SetStatus(new WakeWordStatus(WakeWordState.ModelsMissing, exception.MissingFiles));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (!logged)
                {
                    LogLoadFailed(exception);
                }

                SetStatus(WakeWordStatus.Failed);
            }

            logged = true;
            await Task.Delay(RetryInterval, time, token).ConfigureAwait(false);
        }
    }

    private async Task ListenAsync(IWakeWordDetector detector, CancellationToken token)
    {
        var frame = new float[detector.FrameLength];
        var filled = 0;
        var paused = true;
        var armed = true;
        await foreach (var block in queue.Reader.ReadAllAsync(token).ConfigureAwait(false))
        {
            var offset = 0;
            while (offset < block.Length)
            {
                var count = Math.Min(block.Length - offset, frame.Length - filled);
                block.AsSpan(offset, count).CopyTo(frame.AsSpan(filled));
                offset += count;
                filled += count;
                if (filled < frame.Length)
                {
                    break;
                }

                filled = 0;
                var current = state.Current;
                if (current.Phase == AssistantPhase.Listening || current.MicrophoneMuted)
                {
                    if (!paused)
                    {
                        paused = true;
                        Report(0f);
                        SetStatus(WakeWordStatus.Paused);
                    }

                    continue;
                }

                if (paused)
                {
                    // What was heard before the pause is not part of what comes next.
                    paused = false;
                    detector.Reset();
                    SetStatus(WakeWordStatus.Active);
                }

                var value = detector.Process(frame);
                Report(value);
                if (value < Threshold)
                {
                    armed = true;
                }
                else if (armed)
                {
                    armed = false;
                    Volatile.Write(ref lastDetection, new WakeWordDetection(time.GetUtcNow(), value));
                    LogDetected(value, current.Phase);
                    StatusChanged?.Invoke();
                    await bus.PublishAsync(new ActivationRequested(ActivationSource.WakeWord), token).ConfigureAwait(false);
                }
            }
        }
    }

    private void Report(float value)
    {
        Volatile.Write(ref score, value);
        ScoreReported?.Invoke(value);
    }

    private void SetStatus(WakeWordStatus value)
    {
        var previous = Interlocked.Exchange(ref status, value);
        if (previous.State != value.State || !previous.MissingFiles.SequenceEqual(value.MissingFiles))
        {
            StatusChanged?.Invoke();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Wake word models missing ({Files}); the hotkey and the click still work. Looking again every 30 s.")]
    private partial void LogModelsMissing(string files);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The wake word models could not be loaded. Trying again every 30 s.")]
    private partial void LogLoadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Wake word models found and loaded.")]
    private partial void LogLoadedAfterAll();

    [LoggerMessage(Level = LogLevel.Error, Message = "The wake word stopped; it starts again with Jarvis.")]
    private partial void LogRunFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Wake word detected (score {Score:0.000}, phase {Phase}).")]
    private partial void LogDetected(float score, AssistantPhase phase);
}
