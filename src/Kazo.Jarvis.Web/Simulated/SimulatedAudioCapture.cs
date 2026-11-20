using Kazo.Jarvis.Kernel.Audio;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="IAudioCapture"/> (ADR 0003, section 3): two microphones that can be unplugged and
/// plugged back from the page, and an open stream that delivers a quiet tone whose loudness rises and falls,
/// so the level moves without a real microphone.
/// </summary>
public sealed class SimulatedAudioCapture : IAudioCapture
{
    /// <summary>The default microphone.</summary>
    public static readonly AudioDevice Desk = new("simulated-desk", "Simulated desk microphone", IsDefault: true);

    /// <summary>The other microphone.</summary>
    public static readonly AudioDevice Headset = new("simulated-headset", "Simulated headset", IsDefault: false);

    private static readonly TimeSpan BlockLength = TimeSpan.FromMilliseconds(50);

    private readonly Lock gate = new();
    private readonly HashSet<string> present = [Desk.Id, Headset.Id];
    private Action[] watchers = [];
    private Stream? open;

    /// <summary>Raised when a stream opens or closes, or a device is plugged or unplugged.</summary>
    public event EventHandler? Changed;

    /// <summary>The device of the open stream, or <see langword="null"/> when none is open.</summary>
    public AudioDevice? OpenDevice
    {
        get
        {
            lock (gate)
            {
                return open?.Device;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AudioDevice> ListMicrophones()
    {
        lock (gate)
        {
            return [.. new[] { Desk, Headset }.Where(device => present.Contains(device.Id))];
        }
    }

    /// <inheritdoc />
    public IAudioCaptureStream Open(string? deviceId, SamplesHandler samples, Action<CaptureEnd> ended)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(ended);
        Stream stream;
        lock (gate)
        {
            var device = ListMicrophones().FirstOrDefault(candidate => deviceId is null ? candidate.IsDefault : candidate.Id == deviceId)
                ?? throw new AudioDeviceUnavailableException($"The simulated microphone '{deviceId ?? "default"}' is not plugged in.");
            stream = new Stream(this, device, samples, ended);
            open = stream;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return stream;
    }

    /// <inheritdoc />
    public IDisposable WatchDevices(Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        lock (gate)
        {
            watchers = [.. watchers, changed];
        }

        return new Subscription(() =>
        {
            lock (gate)
            {
                watchers = [.. watchers.Where(existing => existing != changed)];
            }
        });
    }

    /// <summary>Unplugs every microphone: the open stream ends as if the cable were pulled.</summary>
    public void UnplugAll()
    {
        Stream? lost;
        lock (gate)
        {
            present.Clear();
            lost = open;
        }

        lost?.End(new CaptureEnd(CaptureEndReason.DeviceLost, "Unplugged from the Web page."));
        Notify();
    }

    /// <summary>Plugs both microphones back.</summary>
    public void PlugAll()
    {
        lock (gate)
        {
            present.UnionWith([Desk.Id, Headset.Id]);
        }

        Notify();
    }

    private void Notify()
    {
        Action[] targets;
        lock (gate)
        {
            targets = watchers;
        }

        foreach (var watcher in targets)
        {
            watcher();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Closed(Stream stream)
    {
        lock (gate)
        {
            if (open == stream)
            {
                open = null;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Stream : IAudioCaptureStream
    {
        private readonly SimulatedAudioCapture owner;
        private readonly SamplesHandler samples;
        private readonly Action<CaptureEnd> ended;
        private readonly Timer timer;
        private readonly float[] block = new float[MicrophoneFormat.SamplesFor(BlockLength)];
        private long position;
        private int closed;

        public Stream(SimulatedAudioCapture owner, AudioDevice device, SamplesHandler samples, Action<CaptureEnd> ended)
        {
            this.owner = owner;
            this.samples = samples;
            this.ended = ended;
            Device = device;
            timer = new Timer(_ => Deliver(), null, BlockLength, BlockLength);
        }

        public AudioDevice Device { get; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref closed, 1) == 0)
            {
                timer.Dispose();
                owner.Closed(this);
            }
        }

        public void End(CaptureEnd end)
        {
            if (Interlocked.Exchange(ref closed, 1) == 0)
            {
                timer.Dispose();
                owner.Closed(this);
                ended(end);
            }
        }

        private void Deliver()
        {
            if (Volatile.Read(ref closed) == 1)
            {
                return;
            }

            for (var i = 0; i < block.Length; i++, position++)
            {
                var seconds = (double)position / MicrophoneFormat.SampleRate;

                // A 220 Hz tone whose loudness swings twice a second, between about -46 and -6 dB.
                var loudness = 0.25 + (0.24 * Math.Sin(2 * Math.PI * 0.5 * seconds));
                block[i] = (float)(loudness * Math.Sin(2 * Math.PI * 220 * seconds));
            }

            samples(block);
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                dispose();
            }
        }
    }
}
