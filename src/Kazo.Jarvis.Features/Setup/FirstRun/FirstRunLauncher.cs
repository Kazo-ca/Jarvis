using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Setup.FirstRun;

/// <summary>
/// Opens the first-run wizard (US1.4): by itself at start when there is no <c>config.json</c> or the wizard was left
/// unfinished, and when the user asks for it again (tray menu). Its presence enables that menu entry.
/// </summary>
public sealed partial class FirstRunLauncher : IHostedService, IEventHandler<FirstRunWizardRequested>
{
    /// <summary>Key of the wizard window: one at a time.</summary>
    public const string DialogKey = "Setup.FirstRun";

    private readonly ISettingsStore settings;
    private readonly IDialogService dialogs;
    private readonly FirstRunWizardState state;
    private readonly IStringLocalizer<FirstRunWizard> texts;
    private readonly ILogger<FirstRunLauncher> logger;

    /// <summary>Creates the launcher.</summary>
    /// <param name="settings">Tells whether this is a first run, and keeps the wizard pending.</param>
    /// <param name="dialogs">Opens the wizard window.</param>
    /// <param name="state">The step shown, reset when the wizard opens.</param>
    /// <param name="texts">The title of the window.</param>
    /// <param name="logger">Logs the opening.</param>
    public FirstRunLauncher(
        ISettingsStore settings,
        IDialogService dialogs,
        FirstRunWizardState state,
        IStringLocalizer<FirstRunWizard> texts,
        ILogger<FirstRunLauncher> logger)
    {
        this.settings = settings;
        this.dialogs = dialogs;
        this.state = state;
        this.texts = texts;
        this.logger = logger;
    }

    /// <summary>Whether the wizard opens by itself: no settings file, or a wizard left unfinished.</summary>
    public bool IsDue => settings.State.IsFirstRun || settings.Read(FirstRunOptions.Section).Pending;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsDue)
        {
            return;
        }

        LogFirstRun(settings.State.Outcome);

        // Written before the first step: closing the window half-way brings the wizard back at the next start.
        if (!settings.Read(FirstRunOptions.Section).Pending)
        {
            await settings.WriteAsync(FirstRunOptions.Section, new FirstRunOptions { Pending = true }, cancellationToken).ConfigureAwait(false);
        }

        Open();
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask HandleAsync(FirstRunWizardRequested message, CancellationToken cancellationToken)
    {
        Open();
        return ValueTask.CompletedTask;
    }

    private void Open()
    {
        if (!dialogs.IsOpen(DialogKey))
        {
            state.Step = 0;
        }

        dialogs.Open(new DialogRequest(DialogKey, typeof(FirstRunWizard), texts["WindowTitle"]));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "First-run wizard opened (settings: {Outcome}).")]
    private partial void LogFirstRun(SettingsLoadOutcome outcome);
}
