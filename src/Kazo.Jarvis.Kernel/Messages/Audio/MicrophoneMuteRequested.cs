using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Audio;

/// <summary>
/// The user asked to mute or unmute the microphone (tray menu, settings page; US1.13). The audio slice closes
/// or opens the stream, then publishes <see cref="Assistant.MicrophoneMuteChanged"/>. Until a slice handles
/// this event, the host greys the menu entry out.
/// </summary>
/// <param name="Muted">The wanted value.</param>
public sealed record MicrophoneMuteRequested(bool Muted) : IEvent;
