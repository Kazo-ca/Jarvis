using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.Localization;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>
/// Opens the conversation window when the user asks for it (tray menu, US1.2, US1.14). Its presence enables the
/// Conversation entry of the tray menu.
/// </summary>
public sealed class ConversationWindowOpener : IEventHandler<ConversationWindowRequested>
{
    /// <summary>Key of the conversation window: one window at a time.</summary>
    public const string DialogKey = "Conversation.Window";

    private readonly IDialogService dialogs;
    private readonly IStringLocalizer<ConversationWindow> texts;

    /// <summary>Creates the handler.</summary>
    /// <param name="dialogs">Opens the window.</param>
    /// <param name="texts">The title of the window.</param>
    public ConversationWindowOpener(IDialogService dialogs, IStringLocalizer<ConversationWindow> texts)
    {
        this.dialogs = dialogs;
        this.texts = texts;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(ConversationWindowRequested message, CancellationToken cancellationToken)
    {
        dialogs.Open(new DialogRequest(DialogKey, typeof(ConversationWindow), texts["WindowTitle"]));
        return ValueTask.CompletedTask;
    }
}
