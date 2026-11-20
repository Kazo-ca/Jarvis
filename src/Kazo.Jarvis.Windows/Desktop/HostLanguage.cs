using Kazo.Jarvis.Kernel.Messages.Localization;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Windows.Desktop;

/// <summary>
/// Keeps the texts of the host (tray menu, messages) in the language of the assistant (ADR 0008, section 1).
/// The tray menu reads them each time it opens, so a change shows without a restart.
/// </summary>
internal sealed class HostLanguage : IEventHandler<AssistantLanguageChanged>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(AssistantLanguageChanged message, CancellationToken cancellationToken)
    {
        HostTexts.Culture = message.Current.Culture;
        return ValueTask.CompletedTask;
    }
}
