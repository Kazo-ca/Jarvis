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
    private readonly SettingsWindowState state;
    private readonly IStringLocalizer<SettingsWindow> texts;

    /// <summary>Creates the handler.</summary>
    /// <param name="dialogs">Opens the window.</param>
    /// <param name="state">The selected page, set when the request names one.</param>
    /// <param name="texts">The title of the window.</param>
    public SettingsWindowOpener(IDialogService dialogs, SettingsWindowState state, IStringLocalizer<SettingsWindow> texts)
    {
        this.dialogs = dialogs;
        this.state = state;
        this.texts = texts;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(SettingsWindowRequested message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.PageKey is { } page)
        {
            // Before opening: a new window starts on this page, an open one switches to it.
            state.SelectedKey = page;
        }

        // The title bar is translated when the window opens; its content follows later language changes.
        dialogs.Open(new DialogRequest(DialogKey, typeof(SettingsWindow), texts["WindowTitle"]));
        return ValueTask.CompletedTask;
    }
}
