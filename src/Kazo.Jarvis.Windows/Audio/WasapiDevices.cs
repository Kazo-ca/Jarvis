using Kazo.Jarvis.Kernel.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Kazo.Jarvis.Windows.Audio;

/// <summary>
/// The Windows audio endpoints through NAudio (WASAPI): lists them, finds one, and watches their changes.
/// Every call runs on a thread-pool thread: the Core Audio objects must not be created on the UI thread (STA)
/// then used by the audio threads.
/// </summary>
internal sealed class WasapiDevices : IDisposable
{
    private readonly MMDeviceEnumerator enumerator;
    private readonly MMDeviceNotificationClient notifications;
    private readonly Lock gate = new();
    private Action[] watchers = [];

    /// <summary>Creates the enumerator and subscribes to the endpoint notifications.</summary>
    public WasapiDevices()
    {
        (enumerator, notifications) = OnPool(() =>
        {
            var created = new MMDeviceEnumerator();

            // false: raised on the audio worker thread, which must not be held; watchers return quickly.
            var client = created.CreateNotificationClient(useSynchronizationContext: false);
            return (created, client);
        });
        notifications.DeviceAdded += (_, _) => Notify();
        notifications.DeviceRemoved += (_, _) => Notify();
        notifications.DeviceStateChanged += (_, _) => Notify();
        notifications.DefaultDeviceChanged += (_, _) => Notify();
    }

    /// <summary>Runs <paramref name="work"/> on a thread-pool thread (MTA) and waits for it.</summary>
    /// <typeparam name="T">The result.</typeparam>
    /// <param name="work">Core Audio work.</param>
    /// <returns>Its result.</returns>
    public static T OnPool<T>(Func<T> work) =>
        Thread.CurrentThread.GetApartmentState() == ApartmentState.STA || SynchronizationContext.Current is not null
            ? Task.Run(work).GetAwaiter().GetResult()
            : work();

    /// <summary>The active endpoints of <paramref name="flow"/>, the default one marked.</summary>
    /// <param name="flow">Capture or render.</param>
    /// <returns>An empty list when there is none.</returns>
    public IReadOnlyList<AudioDevice> List(DataFlow flow) => OnPool<IReadOnlyList<AudioDevice>>(() =>
    {
        var defaultId = DefaultId(flow);
        var devices = new List<AudioDevice>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            using (device)
            {
                devices.Add(new AudioDevice(device.ID, device.FriendlyName, device.ID == defaultId));
            }
        }

        return devices;
    });

    /// <summary>
    /// The endpoint <paramref name="deviceId"/> when it is active, otherwise the default one. Called on a
    /// thread-pool thread; the caller disposes it.
    /// </summary>
    /// <param name="flow">Capture or render.</param>
    /// <param name="deviceId">The wanted endpoint, or <see langword="null"/> for the default one.</param>
    /// <param name="fallBackToDefault">Whether a missing <paramref name="deviceId"/> falls back on the default one.</param>
    /// <returns>The endpoint.</returns>
    /// <exception cref="AudioDeviceUnavailableException">No such endpoint.</exception>
    public MMDevice Find(DataFlow flow, string? deviceId, bool fallBackToDefault)
    {
        if (deviceId is not null)
        {
            try
            {
                var device = enumerator.GetDevice(deviceId);
                if (device.State == DeviceState.Active)
                {
                    return device;
                }

                device.Dispose();
            }
            catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or ArgumentException)
            {
                // Unknown identifier: the device was removed.
            }

            if (!fallBackToDefault)
            {
                throw new AudioDeviceUnavailableException($"The audio device '{deviceId}' is not available.");
            }
        }

        return enumerator.TryGetDefaultAudioEndpoint(flow, Role.Console, out var fallback)
            ? fallback
            : throw new AudioDeviceUnavailableException(flow == DataFlow.Capture ? "No microphone is connected." : "No audio output is connected.");
    }

    /// <summary>Calls <paramref name="changed"/> after each endpoint change.</summary>
    /// <param name="changed">Called on the audio worker thread; must return quickly.</param>
    /// <returns>Disposing it ends the subscription.</returns>
    public IDisposable Watch(Action changed)
    {
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

    /// <summary>How the samples of <paramref name="format"/> are written.</summary>
    /// <param name="format">A device format.</param>
    /// <returns>The encoding.</returns>
    /// <exception cref="AudioDeviceUnavailableException">A format the converter does not read.</exception>
    public static SampleEncoding EncodingOf(WaveFormat format)
    {
        var isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat
            || (format is WaveFormatExtensible extensible && extensible.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT);
        return (isFloat, format.BitsPerSample) switch
        {
            (true, 32) => SampleEncoding.Float32,
            (false, 16) => SampleEncoding.Pcm16,
            (false, 24) => SampleEncoding.Pcm24,
            (false, 32) => SampleEncoding.Pcm32,
            _ => throw new AudioDeviceUnavailableException($"Unsupported device format: {format}."),
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        notifications.Dispose();
        enumerator.Dispose();
    }

    private string? DefaultId(DataFlow flow)
    {
        if (!enumerator.TryGetDefaultAudioEndpoint(flow, Role.Console, out var device))
        {
            return null;
        }

        using (device)
        {
            return device.ID;
        }
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
