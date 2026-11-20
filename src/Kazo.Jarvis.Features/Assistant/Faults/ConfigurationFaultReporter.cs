using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Assistant.Faults;

/// <summary>
/// Raises <see cref="FaultCause.ConfigurationUnreadable"/> at start when <c>config.json</c> could not be used
/// (ADR 0004, section 2): unreadable, written by a newer version, or a failed migration. Jarvis then runs on
/// default values and saves nothing, which the user must know. The cause only goes away with a restart on a
/// usable file, or a successful turn (ADR 0005, section 3).
/// </summary>
public sealed partial class ConfigurationFaultReporter : IHostedService
{
    private readonly ISettingsStore settings;
    private readonly IMessageBus bus;
    private readonly ILogger<ConfigurationFaultReporter> logger;

    /// <summary>Creates the reporter.</summary>
    /// <param name="settings">Tells how the settings file was loaded.</param>
    /// <param name="bus">Where the fault is raised.</param>
    /// <param name="logger">Logs the fault.</param>
    public ConfigurationFaultReporter(ISettingsStore settings, IMessageBus bus, ILogger<ConfigurationFaultReporter> logger)
    {
        this.settings = settings;
        this.bus = bus;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => ReportAsync(cancellationToken).AsTask();

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Raises the fault when the settings cannot be saved. Called at start; the Web host calls it again to simulate the cause.</summary>
    /// <param name="cancellationToken">Cancels the publication.</param>
    /// <returns>A task that completes once published.</returns>
    public async ValueTask ReportAsync(CancellationToken cancellationToken = default)
    {
        var state = settings.State;
        if (state.CanSave)
        {
            return;
        }

        LogUnusable(state.Outcome, state.FileVersion);
        var fault = new AssistantFault(FaultCause.ConfigurationUnreadable, FaultAdvice.ConfigurationDetail(state.Outcome), FaultOrigin.Settings);
        await bus.PublishAsync<AssistantInput>(new FaultRaised(fault), cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The settings file cannot be used ({Outcome}, version {FileVersion}): Jarvis runs on default values and saves nothing.")]
    private partial void LogUnusable(SettingsLoadOutcome outcome, int? fileVersion);
}
