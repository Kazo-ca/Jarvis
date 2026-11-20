using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Updates;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Features.Maintenance.Updates;

/// <summary>Opens the updates window when the user asks for it (tray menu, notice of a new version).</summary>
public sealed class UpdatesWindowOpener : IEventHandler<UpdatesWindowRequested>
{
    /// <summary>Key of the updates window: one window at a time.</summary>
    public const string WindowKey = "Updates.Panel";

    private readonly IDialogService dialogs;
    private readonly UpdateTexts texts;

    /// <summary>Creates the handler.</summary>
    /// <param name="dialogs">The host's windows.</param>
    /// <param name="texts">The title of the window, in the assistant language.</param>
    public UpdatesWindowOpener(IDialogService dialogs, UpdateTexts texts)
    {
        this.dialogs = dialogs;
        this.texts = texts;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(UpdatesWindowRequested message, CancellationToken cancellationToken)
    {
        dialogs.Open(new DialogRequest(WindowKey, typeof(UpdatePanel), texts.WindowTitle));
        return ValueTask.CompletedTask;
    }
}
