using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Desktop.LaunchAtLogin;

/// <summary>Settings of the start with Windows, section <c>Desktop.LaunchAtLogin</c> of <c>config.json</c>.</summary>
public sealed record LaunchAtLoginOptions
{
    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<LaunchAtLoginOptions> Section { get; } = new("Desktop.LaunchAtLogin", () => new LaunchAtLoginOptions());

    /// <summary>Whether Kazo.Jarvis starts when the user signs in. On by default (design assumption no. 3).</summary>
    public bool Enabled { get; init; } = true;
}
