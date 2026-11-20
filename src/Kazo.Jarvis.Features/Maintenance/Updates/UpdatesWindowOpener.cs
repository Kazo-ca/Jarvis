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

    /// <summary>Creates the handler.</summary>
    /// <param name="dialogs">The host's windows.</param>
    public UpdatesWindowOpener(IDialogService dialogs) => this.dialogs = dialogs;

    /// <inheritdoc />
    public ValueTask HandleAsync(UpdatesWindowRequested message, CancellationToken cancellationToken)
    {
        dialogs.Open(new DialogRequest(WindowKey, typeof(UpdatePanel), UpdateTexts.WindowTitle));
        return ValueTask.CompletedTask;
    }
}
