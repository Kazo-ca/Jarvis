using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Windows.Orb;

/// <summary>
/// Remembered position of the orb window (US1.1), section <c>Desktop.OrbWindow</c> of <c>config.json</c>.
/// Belongs to the Windows host: the position only means something there.
/// </summary>
public sealed record OrbPlacement
{
    /// <summary>The section declared by the Windows host.</summary>
    public static SettingsSection<OrbPlacement> Section { get; } = new("Desktop.OrbWindow", () => new OrbPlacement());

    /// <summary>Left edge in device-independent pixels, or <see langword="null"/> before the first move.</summary>
    public double? Left { get; init; }

    /// <summary>Top edge in device-independent pixels, or <see langword="null"/> before the first move.</summary>
    public double? Top { get; init; }
}
