namespace Kazo.Jarvis.Kernel.Audio;

/// <summary>
/// Audio capture (ADR 0003, section 5): list the microphones, open a stream, close it to mute. The Windows
/// host implements it with WASAPI, the Web host simulates it. Callable from any thread.
/// </summary>
public interface IAudioCapture
{
    /// <summary>The microphones usable now, the Windows default among them.</summary>
    /// <returns>An empty list when there is none.</returns>
    IReadOnlyList<AudioDevice> ListMicrophones();

    /// <summary>
    /// Opens a capture stream on <paramref name="deviceId"/>, or on the Windows default microphone when it is
    /// <see langword="null"/>, and starts delivering samples. Disposing the stream closes the device: Windows no
    /// longer shows an application listening, and no sample is captured (US1.13).
    /// </summary>
    /// <param name="deviceId">The device, or <see langword="null"/> for the default one.</param>
    /// <param name="samples">Receives the samples, 16 kHz mono, on a capture thread; must return quickly.</param>
    /// <param name="ended">Called once when the stream stops by itself (device unplugged, driver error), never after a dispose.</param>
    /// <returns>The open stream.</returns>
    /// <exception cref="AudioDeviceUnavailableException">The device is missing or cannot be opened.</exception>
    IAudioCaptureStream Open(string? deviceId, SamplesHandler samples, Action<CaptureEnd> ended);

    /// <summary>Calls <paramref name="changed"/> when a microphone is added, removed, or the default one changes.</summary>
    /// <param name="changed">Called on a thread of the host's choosing; must return quickly.</param>
    /// <returns>Disposing it ends the subscription.</returns>
    IDisposable WatchDevices(Action changed);
}

/// <summary>An open capture stream. Disposing it closes the device; it can be disposed more than once.</summary>
public interface IAudioCaptureStream : IDisposable
{
    /// <summary>The device actually opened (the default one is resolved).</summary>
    AudioDevice Device { get; }
}

/// <summary>Why a capture stream stopped by itself.</summary>
/// <param name="Reason">The kind of end.</param>
/// <param name="Detail">Technical detail for the log, or <see langword="null"/>.</param>
public sealed record CaptureEnd(CaptureEndReason Reason, string? Detail = null);

/// <summary>Kinds of end of a capture stream.</summary>
public enum CaptureEndReason
{
    /// <summary>The device was unplugged or disabled.</summary>
    DeviceLost,

    /// <summary>Another error of the driver or of the audio service.</summary>
    Failed,
}
