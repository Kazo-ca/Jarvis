using System.Windows.Threading;
using Kazo.Jarvis.Kernel.Messages.Updates;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Windows.Tray;

namespace Kazo.Jarvis.Windows.Updates;

/// <summary>Tells the user that a new version was found by the automatic check: a notice of the tray icon.</summary>
internal sealed class UpdateFoundNotice : IEventHandler<UpdateFound>
{
    private readonly TrayIcon tray;
    private readonly Dispatcher dispatcher;

    /// <summary>Creates the handler.</summary>
    public UpdateFoundNotice(TrayIcon tray, Dispatcher dispatcher)
    {
        this.tray = tray;
        this.dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(UpdateFound message, CancellationToken cancellationToken)
    {
        dispatcher.BeginInvoke(() => tray.ShowUpdateNotice(message.Version));
        return ValueTask.CompletedTask;
    }
}
