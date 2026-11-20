using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.Localization;

namespace Kazo.Jarvis.Features.Settings.Window;

/// <summary>
/// Opens the settings window when the user asks for it (tray menu, US1.2). Its presence enables the
/// Settings entry of the tray menu.
/// </summary>
public sealed class SettingsWindowOpener : IEventHandler<SettingsWindowRequested>
{
    /// <summary>Key of the settings window: one window at a time.</summary>
    public const string DialogKey = "Settings.Window";

    private readonly IDialogService dialogs;
    private readonly IStringLocalizer<SettingsWindow> texts;

    /// <summary>Creates the handler.</summary>
    /// <param name="dialogs">Opens the window.</param>
    /// <param name="texts">The title of the window.</param>
    public SettingsWindowOpener(IDialogService dialogs, IStringLocalizer<SettingsWindow> texts)
    {
        this.dialogs = dialogs;
        this.texts = texts;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(SettingsWindowRequested message, CancellationToken cancellationToken)
    {
        // The title bar is translated when the window opens; its content follows later language changes.
        dialogs.Open(new DialogRequest(DialogKey, typeof(SettingsWindow), texts["WindowTitle"]));
        return ValueTask.CompletedTask;
    }
}
