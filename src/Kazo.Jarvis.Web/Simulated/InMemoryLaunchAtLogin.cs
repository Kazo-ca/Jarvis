using Kazo.Jarvis.Kernel.Desktop;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>Simulated <see cref="ILaunchAtLogin"/>: the state lives in memory.</summary>
public sealed class InMemoryLaunchAtLogin : ILaunchAtLogin
{
    /// <inheritdoc />
    public bool IsEnabled { get; private set; }

    /// <inheritdoc />
    public bool SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        return true;
    }
}
