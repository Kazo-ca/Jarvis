using Kazo.Jarvis.Kernel.Audio;

namespace Kazo.Jarvis.Features.Audio.Devices;

/// <summary>What the microphone does now, for the settings page.</summary>
/// <param name="Muted">The user muted it; no stream is open.</param>
/// <param name="Device">The device being listened to, or <see langword="null"/> when none is open.</param>
/// <param name="Missing">No microphone could be opened (the "fault" condition is set).</param>
public sealed record MicrophoneStatus(bool Muted, AudioDevice? Device, bool Missing)
{
    /// <summary>Before the start: nothing open, nothing missing.</summary>
    public static MicrophoneStatus Stopped { get; } = new(false, null, false);
}
