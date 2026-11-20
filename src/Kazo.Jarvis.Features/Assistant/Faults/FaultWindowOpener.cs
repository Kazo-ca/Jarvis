using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.Localization;

namespace Kazo.Jarvis.Features.Assistant.Faults;

/// <summary>
/// Opens the faults window (US1.17): when the user asks for it (tray menu entry shown while a fault lasts), and
/// when an activation is refused because no microphone is usable, so that a click on the orb or the hotkey that
/// does nothing says why. A muted microphone opens nothing: it is the user's choice, and the orb shows it.
/// </summary>
public sealed class FaultWindowOpener : IEventHandler<FaultWindowRequested>, IEventHandler<ActivationRefused>
{
    /// <summary>Key of the faults window: one window at a time.</summary>
    public const string DialogKey = "Assistant.Faults";

    private readonly IDialogService dialogs;
    private readonly IStringLocalizer<FaultWindow> texts;

    /// <summary>Creates the handler.</summary>
    /// <param name="dialogs">Opens the window.</param>
    /// <param name="texts">The title of the window.</param>
    public FaultWindowOpener(IDialogService dialogs, IStringLocalizer<FaultWindow> texts)
    {
        this.dialogs = dialogs;
        this.texts = texts;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(FaultWindowRequested message, CancellationToken cancellationToken)
    {
        Open();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(ActivationRefused message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The wake word cannot be heard without a microphone: only the hotkey and the click get here.
        if (message.Reason == ActivationRefusal.MicrophoneMissing)
        {
            Open();
        }

        return ValueTask.CompletedTask;
    }

    private void Open() => dialogs.Open(new DialogRequest(DialogKey, typeof(FaultWindow), texts["WindowTitle"]));
}
