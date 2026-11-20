using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Audio.Devices;

/// <summary>Settings of the audio devices (US1.13), section <c>Audio.Devices</c> of <c>config.json</c>.</summary>
public sealed record AudioDevicesOptions
{
    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<AudioDevicesOptions> Section { get; } = new("Audio.Devices", () => new AudioDevicesOptions());

    /// <summary>
    /// The chosen microphone, or <see langword="null"/> for the Windows default. When it is missing, the
    /// Windows default is used until it comes back.
    /// </summary>
    public string? MicrophoneId { get; init; }

    /// <summary>The chosen output, or <see langword="null"/> for the Windows default.</summary>
    public string? OutputId { get; init; }

    /// <summary>
    /// Whether the microphone is muted. Kept across restarts: a microphone the user muted does not open again
    /// by itself (choice of work item stage1/8-audio-devices, provisional).
    /// </summary>
    public bool MicrophoneMuted { get; init; }
}
