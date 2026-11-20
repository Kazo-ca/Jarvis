using Kazo.Jarvis.Kernel.Audio;

namespace Kazo.Jarvis.Tests.Unit.Features.Audio;

/// <summary>A capture whose microphones the test plugs and unplugs, and whose stream the test feeds.</summary>
internal sealed class FakeAudioCapture : IAudioCapture
{
    public static readonly AudioDevice Desk = new("desk", "Desk microphone", IsDefault: true);
    public static readonly AudioDevice Headset = new("headset", "Headset", IsDefault: false);

    private readonly Lock gate = new();
    private Action[] watchers = [];

    public List<AudioDevice> Present { get; } = [Desk, Headset];

    public List<string?> Opened { get; } = [];

    public FakeStream? Current { get; private set; }

    public bool RefuseOpening { get; set; }

    public IReadOnlyList<AudioDevice> ListMicrophones()
    {
        lock (gate)
        {
            return [.. Present];
        }
    }

    public IAudioCaptureStream Open(string? deviceId, SamplesHandler samples, Action<CaptureEnd> ended)
    {
        lock (gate)
        {
            Opened.Add(deviceId);
            var device = Present.FirstOrDefault(candidate => deviceId is null ? candidate.IsDefault : candidate.Id == deviceId);
            if (device is null || RefuseOpening)
            {
                throw new AudioDeviceUnavailableException($"'{deviceId}' cannot be opened.");
            }

            Current = new FakeStream(device, samples, ended);
            return Current;
        }
    }

    public IDisposable WatchDevices(Action changed)
    {
        lock (gate)
        {
            watchers = [.. watchers, changed];
        }

        return new Unsubscribe(() =>
        {
            lock (gate)
            {
                watchers = [.. watchers.Where(existing => existing != changed)];
            }
        });
    }

    /// <summary>Removes a device; its open stream ends as when the cable is pulled.</summary>
    public void Unplug(AudioDevice device)
    {
        FakeStream? lost;
        lock (gate)
        {
            Present.Remove(device);
            lost = Current is { Disposed: false } open && open.Device == device ? open : null;
        }

        lost?.EndByItself(new CaptureEnd(CaptureEndReason.DeviceLost, "unplugged"));
        Notify();
    }

    public void Plug(AudioDevice device)
    {
        lock (gate)
        {
            Present.Add(device);
        }

        Notify();
    }

    private void Notify()
    {
        foreach (var watcher in watchers)
        {
            watcher();
        }
    }

    internal sealed class FakeStream(AudioDevice device, SamplesHandler samples, Action<CaptureEnd> ended) : IAudioCaptureStream
    {
        public AudioDevice Device => device;

        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;

        public void Deliver(float[] block) => samples(block);

        public void EndByItself(CaptureEnd end)
        {
            Disposed = true;
            ended(end);
        }
    }

    private sealed class Unsubscribe(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}

/// <summary>A playback that records the selected output and the clips.</summary>
internal sealed class FakeAudioPlayback : IAudioPlayback
{
    public List<string?> Selections { get; } = [];

    public List<AudioClip> Played { get; } = [];

    public int Stops { get; private set; }

    public string? SelectedOutput { get; private set; }

    public IReadOnlyList<AudioDevice> ListOutputs() => [new AudioDevice("speakers", "Speakers", IsDefault: true)];

    public void SelectOutput(string? deviceId)
    {
        Selections.Add(deviceId);
        SelectedOutput = deviceId;
    }

    public ValueTask PlayAsync(AudioClip clip, CancellationToken cancellationToken = default)
    {
        Played.Add(clip);
        return ValueTask.CompletedTask;
    }

    public void StopAll() => Stops++;
}
