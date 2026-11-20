using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Desktop;

/// <summary>
/// The user asked for the settings window (tray menu, US1.2). The settings slice opens it; until a slice
/// handles this event, the host greys the menu entry out.
/// </summary>
public sealed record SettingsWindowRequested : IEvent;

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
