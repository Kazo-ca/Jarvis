using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Desktop.LaunchAtLogin;

/// <summary>
/// Background service of the start with Windows: applies the setting at start-up (so a moved installation
/// registers its new path) and each time it changes.
/// </summary>
public sealed partial class LaunchAtLoginBinder : IHostedService, IDisposable
{
    private readonly ILaunchAtLogin launchAtLogin;
    private readonly ISettingsStore settings;
    private readonly ILogger<LaunchAtLoginBinder> logger;
    private IDisposable? watch;

    /// <summary>Creates the binder.</summary>
    /// <param name="launchAtLogin">The host's start at sign-in.</param>
    /// <param name="settings">The settings, for <see cref="LaunchAtLoginOptions.Section"/>.</param>
    /// <param name="logger">Logs what was applied.</param>
    public LaunchAtLoginBinder(ILaunchAtLogin launchAtLogin, ISettingsStore settings, ILogger<LaunchAtLoginBinder> logger)
    {
        this.launchAtLogin = launchAtLogin;
        this.settings = settings;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        watch = settings.Watch(LaunchAtLoginOptions.Section, Apply);
        Apply(settings.Read(LaunchAtLoginOptions.Section));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        watch?.Dispose();
        watch = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => watch?.Dispose();

    private void Apply(LaunchAtLoginOptions options)
    {
        try
        {
            var applied = launchAtLogin.SetEnabled(options.Enabled);
            LogApplied(options.Enabled, applied);
        }
        catch (Exception exception)
        {
            // A refused registry write must not stop the other services.
            LogFailed(options.Enabled, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Start with Windows wanted: {Enabled}; applied by the host: {Applied}.")]
    private partial void LogApplied(bool enabled, bool applied);

    [LoggerMessage(Level = LogLevel.Error, Message = "Start with Windows could not be set to {Enabled}.")]
    private partial void LogFailed(bool enabled, Exception exception);
}
