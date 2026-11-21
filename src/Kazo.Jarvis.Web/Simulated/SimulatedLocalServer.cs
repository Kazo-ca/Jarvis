using Kazo.Jarvis.Kernel.LocalServer;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// A local server that opens no port (ADR 0003, section 3): it says it listens on the port of the settings, so
/// that the pages show what the Windows host shows. Nothing can call it.
/// </summary>
public sealed class SimulatedLocalServer : ILocalServer, IDisposable
{
    private readonly ISettingsStore settings;
    private readonly IDisposable watch;

    /// <summary>Creates the simulated server.</summary>
    /// <param name="settings">The settings, for the port.</param>
    public SimulatedLocalServer(ISettingsStore settings)
    {
        this.settings = settings;
        watch = settings.Watch(LocalServerOptions.Section, _ => Changed?.Invoke());
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public LocalServerStatus Status => new(true, settings.Read(LocalServerOptions.Section).Port);

    /// <inheritdoc />
    public string SessionToken => "simulated-session-token";

    /// <inheritdoc />
    public void Dispose() => watch.Dispose();
}
