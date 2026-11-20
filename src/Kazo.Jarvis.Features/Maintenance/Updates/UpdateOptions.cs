using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Maintenance.Updates;

/// <summary>Settings of the updates slice, section <c>Maintenance.Updates</c> of <c>config.json</c>.</summary>
public sealed record UpdateOptions
{
    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<UpdateOptions> Section { get; } = new("Maintenance.Updates", () => new UpdateOptions());

    /// <summary>
    /// Whether Jarvis asks GitHub for a new version on its own (a minute after start, then every 6 hours).
    /// On by default (Benoit's choice, November 20, 2026): the request only asks for the list of releases, and
    /// it is the one exception to "no connection leaves the PC by default" (stage 1 plan, work item 17).
    /// </summary>
    public bool Automatic { get; init; } = true;

    /// <summary>Whether pre-releases are offered. On while Kazo.Jarvis is in 0.x and only publishes pre-releases.</summary>
    public bool IncludePreReleases { get; init; } = true;
}
