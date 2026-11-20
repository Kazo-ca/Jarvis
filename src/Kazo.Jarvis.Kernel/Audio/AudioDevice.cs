namespace Kazo.Jarvis.Kernel.Audio;

/// <summary>A microphone or an audio output, as the host lists it (ADR 0003, section 5).</summary>
/// <param name="Id">Stable identifier given by the host; kept in the settings to find the device again.</param>
/// <param name="Name">Name shown to the user, as Windows gives it.</param>
/// <param name="IsDefault">Whether Windows uses it by default.</param>
public sealed record AudioDevice(string Id, string Name, bool IsDefault);
