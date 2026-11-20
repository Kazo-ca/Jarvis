using System.Runtime.InteropServices;
using Kazo.Jarvis.Kernel.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Kazo.Jarvis.Windows.Audio;

/// <summary>
/// <see cref="IAudioPlayback"/> on WASAPI in shared mode (ADR 0003, section 5): each clip is converted to the
/// output's own format (<see cref="PlaybackConverter"/>) and played by its own player, so Windows mixes clips
/// played together. <see cref="StopAll"/> stops them at once (US1.12).
/// </summary>
internal sealed class WasapiAudioPlayback : IAudioPlayback
{
    // Latency of the shared-mode buffer: low enough for an interruption to sound immediate.
    private const int LatencyMilliseconds = 60;

    private readonly WasapiDevices devices;
    private readonly Lock gate = new();
    private CancellationTokenSource stopAll = new();
    private string? selected;

    /// <summary>Creates the playback.</summary>
    /// <param name="devices">The Windows audio endpoints.</param>
    public WasapiAudioPlayback(WasapiDevices devices) => this.devices = devices;

    /// <inheritdoc />
    public string? SelectedOutput
    {
        get
        {
            lock (gate)
            {
                return selected;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AudioDevice> ListOutputs() => devices.List(DataFlow.Render);

    /// <inheritdoc />
    public void SelectOutput(string? deviceId)
    {
        lock (gate)
        {
            selected = deviceId;
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayAsync(AudioClip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);
        string? deviceId;
        CancellationToken stop;
        lock (gate)
        {
            deviceId = selected;
            stop = stopAll.Token;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stop);
        if (linked.IsCancellationRequested || clip.Samples.IsEmpty)
        {
            return;
        }

        // On the thread pool: no synchronization context is captured by the player, and the conversion of a
        // long sentence does not hold the caller.
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var player = await Task.Run(() => Start(clip, deviceId, finished), CancellationToken.None).ConfigureAwait(false);
        using (linked.Token.Register(() => Interrupt(player.Output, finished)))
        {
            await finished.Task.ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void StopAll()
    {
        CancellationTokenSource previous;
        lock (gate)
        {
            previous = stopAll;
            stopAll = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }

    // Stop at once (US1.12); the clip's task completes even if the player raises no event after a stop.
    private static void Interrupt(WasapiPlayer player, TaskCompletionSource finished)
    {
        try
        {
            player.Stop();
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException)
        {
            // Already stopped, or the device went away: nothing left to stop.
        }

        finished.TrySetResult();
    }

    private Player Start(AudioClip clip, string? deviceId, TaskCompletionSource finished)
    {
        // The player keeps the device; the device object is left to it and to the garbage collector, as NAudio
        // does not say whether it releases it.
        var device = devices.Find(DataFlow.Render, deviceId, fallBackToDefault: true);
        WasapiPlayer? player = null;
        try
        {
            player = new WasapiPlayerBuilder()
                .WithDevice(device)
                .WithSharedMode()
                .WithEventSync()
                .WithLatency(LatencyMilliseconds)
                .Build();

            // The mix format is always accepted in shared mode; the converter matches its rate and channels.
            var mix = player.DeviceMixFormat;
            var samples = PlaybackConverter.Convert(clip.Samples.Span, clip.SampleRate, mix.SampleRate, mix.Channels);
            player.PlaybackStopped += (_, _) => finished.TrySetResult();
            player.Init(new FloatWaveProvider(samples, WaveFormat.CreateIeeeFloatWaveFormat(mix.SampleRate, mix.Channels)));
            player.Play();
            return new Player(player);
        }
        catch (Exception exception)
        {
            player?.Dispose();
            throw exception as AudioDeviceUnavailableException
                ?? new AudioDeviceUnavailableException("The audio output could not be opened.", exception);
        }
    }

    private sealed class Player(WasapiPlayer output) : IDisposable
    {
        public WasapiPlayer Output => output;

        public void Dispose() => output.Dispose();
    }

    /// <summary>Hands the converted samples to the player as bytes; returns 0 at the end, which stops it.</summary>
    private sealed class FloatWaveProvider(float[] samples, WaveFormat format) : IWaveProvider
    {
        private int position;

        public WaveFormat WaveFormat => format;

        public int Read(Span<byte> buffer)
        {
            var source = MemoryMarshal.AsBytes(samples.AsSpan());
            var count = Math.Min(buffer.Length, source.Length - position);
            source.Slice(position, count).CopyTo(buffer);
            position += count;
            return count;
        }
    }
}
