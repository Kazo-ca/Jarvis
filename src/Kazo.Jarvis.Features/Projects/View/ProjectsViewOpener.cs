using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.Localization;

namespace Kazo.Jarvis.Features.Projects.View;

/// <summary>
/// Answers the double click on the orb (ADR 0027): opens the projects view over the whole screen, where the user
/// left it (ADR 0026, section 8).
/// </summary>
public sealed class ProjectsViewOpener : IEventHandler<ProjectsViewRequested>
{
    /// <summary>The key of the window: one view at most.</summary>
    public const string DialogKey = "Projects.View";

    private readonly IDialogService dialogs;
    private readonly IStringLocalizer<ProjectsView> texts;

    /// <summary>Creates the opener.</summary>
    /// <param name="dialogs">The windows of the host.</param>
    /// <param name="texts">The title of the window.</param>
    public ProjectsViewOpener(IDialogService dialogs, IStringLocalizer<ProjectsView> texts)
    {
        this.dialogs = dialogs;
        this.texts = texts;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(ProjectsViewRequested message, CancellationToken cancellationToken)
    {
        dialogs.Open(new DialogRequest(DialogKey, typeof(ProjectsView), texts["WindowTitle"]) { Maximized = true });
        return ValueTask.CompletedTask;
    }
}
