using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Desktop;

/// <summary>
/// The user asked for the settings window (tray menu, US1.2). The settings slice opens it; until a slice
/// handles this event, the host greys the menu entry out.
/// </summary>
/// <param name="PageKey">
/// The page to show (a <see cref="Settings.SettingsPage.Key"/>), for instance the setting that fixes a fault
/// (US1.17); <see langword="null"/> keeps the page last shown.
/// </param>
public sealed record SettingsWindowRequested(string? PageKey = null) : IEvent;

/// <summary>
/// The user asked to see the current fault and what to do about it (US1.17): tray menu entry shown while a fault
/// lasts, or an activation refused because no microphone is usable. The faults slice opens its window.
/// </summary>
public sealed record FaultWindowRequested : IEvent;

/// <summary>
/// The user asked for the conversation window (tray menu, US1.2). The conversation slice opens it; until a
/// slice handles this event, the host greys the menu entry out.
/// </summary>
public sealed record ConversationWindowRequested : IEvent;

/// <summary>
/// The user asked for the first-run wizard again (tray menu, stage 1 plan, work item 14). The first-run slice
/// opens it; it also opens by itself when there is no settings file. Until a slice handles this event, the host
/// greys the menu entry out.
/// </summary>
public sealed record FirstRunWizardRequested : IEvent;

/// <summary>
/// A right click on the orb (ADR 0027, section 2): the fan slice opens the task fan, or puts it away when it is
/// open. It waits for no delay and never starts listening.
/// </summary>
public sealed record TaskFanToggleRequested : IEvent;

/// <summary>
/// A double click on the orb (ADR 0027, section 3): the slice of the projects view opens it. Never also a single
/// click: the orb slice waits for the double-click delay before it treats a left click. Until a slice handles
/// this event there is no double click and no delay: a left click acts at once.
/// </summary>
public sealed record ProjectsViewRequested : IEvent;
