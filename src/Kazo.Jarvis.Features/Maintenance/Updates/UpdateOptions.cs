using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Maintenance.Updates;

/// <summary>Settings of the updates slice, section <c>Maintenance.Updates</c> of <c>config.json</c>.</summary>
public sealed record UpdateOptions
{
    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<UpdateOptions> Section { get; } = new("Maintenance.Updates", () => new UpdateOptions());

    /// <summary>
    /// Whether Jarvis asks GitHub for a new version on its own (a minute after start, then every 6 hours).
    /// Off by default: with the default settings nothing leaves the PC (stage 1 plan); the user checks from
    /// the tray menu instead. Provisional choice, to be confirmed by Benoit.
    /// </summary>
    public bool Automatic { get; init; }

    /// <summary>Whether pre-releases are offered. On while Kazo.Jarvis is in 0.x and only publishes pre-releases.</summary>
    public bool IncludePreReleases { get; init; } = true;
}
