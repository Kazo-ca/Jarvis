using Kazo.Jarvis.Kernel.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Kazo.Jarvis.Windows.Audio;

/// <summary>
/// <see cref="IAudioCapture"/> on WASAPI in shared mode (ADR 0003, section 5): captures in the device format,
/// converts to 16 kHz mono (<see cref="CaptureConverter"/>). Disposing the stream releases the device, so
/// Windows no longer shows Jarvis as listening (US1.13).
/// </summary>
internal sealed class WasapiAudioCapture : IAudioCapture
{
    // Small buffer: the wake word needs the sound soon (NAudio's default is 100 ms).
    private const int BufferMilliseconds = 50;

    private readonly WasapiDevices devices;

    /// <summary>Creates the capture.</summary>
    /// <param name="devices">The Windows audio endpoints.</param>
    public WasapiAudioCapture(WasapiDevices devices) => this.devices = devices;

    /// <inheritdoc />
    public IReadOnlyList<AudioDevice> ListMicrophones() => devices.List(DataFlow.Capture);

    /// <inheritdoc />
    public IDisposable WatchDevices(Action changed) => devices.Watch(changed);

    /// <inheritdoc />
    public IAudioCaptureStream Open(string? deviceId, SamplesHandler samples, Action<CaptureEnd> ended) =>
        WasapiDevices.OnPool<IAudioCaptureStream>(() =>
        {
            var device = devices.Find(DataFlow.Capture, deviceId, fallBackToDefault: deviceId is null);
            try
            {
                return new Stream(device, samples, ended);
            }
            catch (Exception exception) when (exception is not AudioDeviceUnavailableException)
            {
                throw new AudioDeviceUnavailableException($"The microphone '{device.FriendlyName}' could not be opened.", exception);
            }
        });

    private sealed class Stream : IAudioCaptureStream
    {
        private readonly WasapiRecorder recorder;
        private readonly CaptureConverter converter;
        private readonly SamplesHandler samples;
        private readonly Action<CaptureEnd> ended;
        private readonly List<float> converted = new(4_096);
        private int disposed;

        public Stream(MMDevice device, SamplesHandler samples, Action<CaptureEnd> ended)
        {
            this.samples = samples;
            this.ended = ended;
            Device = new AudioDevice(device.ID, device.FriendlyName, IsDefault: false);

            // Built on a thread-pool thread (no synchronization context): RecordingStopped is raised on the
            // capture thread, never queued behind the UI. The recorder keeps the device; the device object is
            // left to it and to the garbage collector, as NAudio does not say whether it releases it.
            recorder = new WasapiRecorderBuilder()
                .WithDevice(device)
                .WithSharedMode()
                .WithEventSync()
                .WithBufferLength(BufferMilliseconds)
                .Build();
            converter = new CaptureConverter(recorder.WaveFormat.SampleRate, recorder.WaveFormat.Channels, WasapiDevices.EncodingOf(recorder.WaveFormat));
            recorder.DataAvailable += OnData;
            recorder.RecordingStopped += OnStopped;
            recorder.StartRecording();
        }

        public AudioDevice Device { get; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            recorder.DataAvailable -= OnData;
            recorder.RecordingStopped -= OnStopped;

            // Dispose stops the capture thread and releases the audio client: Windows no longer shows Jarvis as listening.
            recorder.Dispose();
        }

        private void OnData(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            converter.Convert(buffer, converted);
            samples(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(converted));
        }

        private void OnStopped(object? sender, StoppedEventArgs e)
        {
            // Only a stop the stream did not ask for: a dispose unsubscribes first.
            if (Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            var reason = e.Exception is System.Runtime.InteropServices.COMException { HResult: DeviceInvalidated }
                ? CaptureEndReason.DeviceLost
                : CaptureEndReason.Failed;
            ended(new CaptureEnd(reason, e.Exception?.Message));
        }

        // AUDCLNT_E_DEVICE_INVALIDATED: the endpoint was unplugged, disabled or reconfigured.
        private const int DeviceInvalidated = unchecked((int)0x88890004);
    }
}
