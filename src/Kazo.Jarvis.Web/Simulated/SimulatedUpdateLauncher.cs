using System.Reflection;
using Kazo.Jarvis.Kernel.Updates;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="IUpdateLauncher"/> (US1.18): records the prepared folder instead of starting the
/// updater, and the page stays open.
/// </summary>
public sealed class SimulatedUpdateLauncher : IUpdateLauncher
{
    /// <inheritdoc />
    public string CurrentVersion { get; } =
        typeof(SimulatedUpdateLauncher).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    /// <inheritdoc />
    public bool CanInstall => true;

    /// <summary>The folder of the last launch, or <see langword="null"/>.</summary>
    public string? LaunchedFolder { get; private set; }

    /// <summary>Raised after a launch.</summary>
    public event EventHandler? Launched;

    /// <inheritdoc />
    public bool Launch(string preparedFolder)
    {
        LaunchedFolder = preparedFolder;
        Launched?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
