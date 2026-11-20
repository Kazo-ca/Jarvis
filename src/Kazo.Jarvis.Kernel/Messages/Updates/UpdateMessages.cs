using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Updates;

/// <summary>
/// The user asked for the updates window (tray menu, or the notice of a new version). The updates slice
/// opens it; until a slice handles this event, the host greys the menu entry out.
/// </summary>
public sealed record UpdatesWindowRequested : IEvent;

/// <summary>
/// A version newer than the running one was found (US1.18). Published once per version; the host tells the
/// user (a notice of the tray icon).
/// </summary>
/// <param name="Version">The new version, for instance <c>0.2.0</c>.</param>
public sealed record UpdateFound(string Version) : IEvent;
