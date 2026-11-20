using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Audio;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Audio.Devices;

/// <summary>
/// Background service of the audio slice: keeps one capture stream open on the right microphone, writes what
/// it hears to the kernel's microphone feed and its level to the orb, and closes it when the user mutes the
/// microphone (US1.13). A microphone unplugged or missing sets the "microphone missing" fault, cleared when one
/// can be opened again.
/// </summary>
/// <remarks>
/// Every change (start, setting, device list, end of a stream, mute request) runs <see cref="ReconcileAsync"/>
/// one at a time: it compares the open stream with the wanted one and fixes the difference.
/// </remarks>
public sealed partial class MicrophoneService : IHostedService, IEventHandler<MicrophoneMuteRequested>, IDisposable
{
    private readonly IAudioCapture capture;
    private readonly ISettingsStore settings;
    private readonly IMessageBus bus;
    private readonly IMicrophoneSink feed;
    private readonly ISoundLevelSink levels;
    private readonly ILogger<MicrophoneService> logger;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly SamplesHandler onSamples;
    private AudioDevicesOptions options = new();
    private IAudioCaptureStream? stream;
    private int generation;
    private bool started;
    private bool faultRaised;
    private bool announcedMuted;
    private IDisposable? settingsWatch;
    private IDisposable? devicesWatch;
    private MicrophoneStatus status = MicrophoneStatus.Stopped;

    /// <summary>Creates the service.</summary>
    /// <param name="capture">The host's audio capture.</param>
    /// <param name="settings">The settings, for <see cref="AudioDevicesOptions.Section"/>.</param>
    /// <param name="bus">Where the mute changes and the faults go.</param>
    /// <param name="feed">Where the samples go (listening buffer of the kernel).</param>
    /// <param name="levels">Where the microphone level goes (the orb).</param>
    /// <param name="logger">Logs the device opened and the failures.</param>
    public MicrophoneService(
        IAudioCapture capture,
        ISettingsStore settings,
        IMessageBus bus,
        IMicrophoneSink feed,
        ISoundLevelSink levels,
        ILogger<MicrophoneService> logger)
    {
        this.capture = capture;
        this.settings = settings;
        this.bus = bus;
        this.feed = feed;
        this.levels = levels;
        this.logger = logger;
        onSamples = OnSamples;
    }

    /// <summary>Raised after each change of <see cref="Status"/>, on any thread.</summary>
    public event Action? StatusChanged;

    /// <summary>What the microphone does now.</summary>
    public MicrophoneStatus Status => Volatile.Read(ref status);

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            options = settings.Read(AudioDevicesOptions.Section);
            started = true;
        }
        finally
        {
            gate.Release();
        }

        settingsWatch = settings.Watch(AudioDevicesOptions.Section, changed => Run(() => ApplyOptionsAsync(changed)));
        devicesWatch = capture.WatchDevices(() => Run(ReconcileAsync));
        await ReconcileAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        settingsWatch?.Dispose();
        devicesWatch?.Dispose();
        settingsWatch = null;
        devicesWatch = null;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            started = false;
            Close();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask HandleAsync(MicrophoneMuteRequested message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var wanted = settings.Read(AudioDevicesOptions.Section) with { MicrophoneMuted = message.Muted };
        var result = await settings.WriteAsync(AudioDevicesOptions.Section, wanted, cancellationToken).ConfigureAwait(false);

        // A saved value comes back through the watch; an unsaved one still applies until the next start.
        if (!result.IsSaved)
        {
            LogMuteNotSaved(result.Outcome);
            await ApplyOptionsAsync(wanted).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        settingsWatch?.Dispose();
        devicesWatch?.Dispose();
        stream?.Dispose();
        gate.Dispose();
    }

    /// <summary>Applies new settings: mute, or another microphone.</summary>
    private async Task ApplyOptionsAsync(AudioDevicesOptions changed)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            options = changed;
        }
        finally
        {
            gate.Release();
        }

        await ReconcileAsync().ConfigureAwait(false);
    }

    /// <summary>Brings the open stream in line with the settings and the devices present.</summary>
    private async Task ReconcileAsync()
    {
        var messages = new List<AssistantInput>();
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!started)
            {
                return;
            }

            if (options.MicrophoneMuted != announcedMuted)
            {
                announcedMuted = options.MicrophoneMuted;
                messages.Add(new MicrophoneMuteChanged(announcedMuted));
            }

            if (options.MicrophoneMuted)
            {
                Close();

                // Nothing is missing while the user does not want the microphone.
                ResolveFault(messages);
                SetStatus(new MicrophoneStatus(true, null, false));
                return;
            }

            var wanted = ChooseDevice(capture.ListMicrophones(), options.MicrophoneId);
            if (wanted is null)
            {
                Close();
                RaiseFault(messages, "No microphone is connected.");
                SetStatus(new MicrophoneStatus(false, null, true));
                return;
            }

            if (stream is { } open && open.Device.Id == wanted.Id)
            {
                ResolveFault(messages);
                SetStatus(new MicrophoneStatus(false, open.Device, false));
                return;
            }

            Close();
            try
            {
                var opening = ++generation;
                stream = capture.Open(wanted.Id, onSamples, end => Run(() => OnEndedAsync(opening, end)));
                LogOpened(stream.Device.Name, wanted.Id == options.MicrophoneId || options.MicrophoneId is null);
                ResolveFault(messages);
                SetStatus(new MicrophoneStatus(false, stream.Device, false));
            }
            catch (AudioDeviceUnavailableException exception)
            {
                LogOpenFailed(wanted.Name, exception);
                RaiseFault(messages, exception.Message);
                SetStatus(new MicrophoneStatus(false, null, true));
            }
        }
        finally
        {
            gate.Release();

            // Published after the release: the state machine's subscribers may read the status.
            foreach (var message in messages)
            {
                await bus.PublishAsync(message, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The chosen microphone when present; otherwise the Windows default; otherwise the first one listed.
    /// </summary>
    /// <param name="devices">The microphones present.</param>
    /// <param name="chosenId">The chosen one, or <see langword="null"/> for the Windows default.</param>
    /// <returns>The device to open, or <see langword="null"/> when there is none.</returns>
    private static AudioDevice? ChooseDevice(IReadOnlyList<AudioDevice> devices, string? chosenId) =>
        devices.FirstOrDefault(device => chosenId is not null && device.Id == chosenId)
        ?? devices.FirstOrDefault(device => device.IsDefault)
        ?? devices.FirstOrDefault();

    private async Task OnEndedAsync(int ended, CaptureEnd end)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // A stream closed since then (mute, other device) is not the one to replace.
            if (ended != generation || stream is null)
            {
                return;
            }

            LogEnded(stream.Device.Name, end.Reason, end.Detail);
            Close();
        }
        finally
        {
            gate.Release();
        }

        // Another microphone may take over; otherwise the fault is set until one comes back.
        await ReconcileAsync().ConfigureAwait(false);
    }

    private void OnSamples(ReadOnlySpan<float> samples)
    {
        feed.Write(samples);
        levels.Report(SoundOrigin.Microphone, MicrophoneLevel.Of(samples));
    }

    private void Close()
    {
        if (stream is null)
        {
            return;
        }

        generation++;
        stream.Dispose();
        stream = null;

        // What was heard does not outlive the stream (ADR 0006, section 1), and the orb does not stay lit.
        feed.Clear();
        levels.Report(SoundOrigin.Microphone, 0f);
    }

    private void RaiseFault(List<AssistantInput> messages, string detail)
    {
        if (!faultRaised)
        {
            faultRaised = true;
            messages.Add(new FaultRaised(new AssistantFault(FaultCause.MicrophoneMissing, detail, FaultOrigin.Microphone)));
        }
    }

    private void ResolveFault(List<AssistantInput> messages)
    {
        if (faultRaised)
        {
            faultRaised = false;
            messages.Add(new FaultResolved(FaultCause.MicrophoneMissing, FaultOrigin.Microphone));
        }
    }

    private void SetStatus(MicrophoneStatus value)
    {
        if (Volatile.Read(ref status) == value)
        {
            return;
        }

        Volatile.Write(ref status, value);
        StatusChanged?.Invoke();
    }

    private void Run(Func<Task> work) => _ = Task.Run(async () =>
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogFailed(exception);
        }
    });

    [LoggerMessage(Level = LogLevel.Information, Message = "Microphone opened: {Device} (chosen one: {Chosen}).")]
    private partial void LogOpened(string device, bool chosen);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microphone {Device} could not be opened.")]
    private partial void LogOpenFailed(string device, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microphone {Device} stopped by itself: {Reason} ({Detail}).")]
    private partial void LogEnded(string device, CaptureEndReason reason, string? detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The microphone mute could not be saved ({Outcome}); it applies until the next start.")]
    private partial void LogMuteNotSaved(SettingsWriteOutcome outcome);

    [LoggerMessage(Level = LogLevel.Error, Message = "The microphone could not be updated.")]
    private partial void LogFailed(Exception exception);
}
