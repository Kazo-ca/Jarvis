using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Windows.Orb;

/// <summary>
/// Remembered position of the orb window (US1.1), section <c>Desktop.OrbWindow</c> of <c>config.json</c>.
/// Belongs to the Windows host: the position only means something there. Kept in physical screen pixels,
/// not in WPF device-independent pixels: with screens of different scale, a WPF position is converted with
/// the scale of the screen the window is created on, and the orb drifted at each restart (Windows check of
/// work item stage1/4-hosts).
/// </summary>
public sealed record OrbPlacement
{
    /// <summary>The section declared by the Windows host.</summary>
    public static SettingsSection<OrbPlacement> Section { get; } = new("Desktop.OrbWindow", () => new OrbPlacement());

    /// <summary>Left edge in physical screen pixels, or <see langword="null"/> before the first move.</summary>
    public int? ScreenX { get; init; }

    /// <summary>Top edge in physical screen pixels, or <see langword="null"/> before the first move.</summary>
    public int? ScreenY { get; init; }
}
