using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Desktop.Hotkey;

/// <summary>Settings of the global hotkey (US1.6), section <c>Desktop.Hotkey</c> of <c>config.json</c>.</summary>
public sealed record HotkeyOptions
{
    /// <summary>The default hotkey. Not set by an ADR: chosen in work item stage1/4-hosts, changeable in the settings.</summary>
    public const string DefaultGesture = "Ctrl+Alt+J";

    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<HotkeyOptions> Section { get; } = new(
        "Desktop.Hotkey",
        () => new HotkeyOptions(),
        value => HotkeyGesture.TryParse(value.Gesture, out _)
            ? []
            : [$"'{value.Gesture}' is not a valid hotkey: Ctrl, Alt or Win, then one letter, digit, F1-F24 or named key."]);

    /// <summary>The hotkey, for instance <c>Ctrl+Alt+J</c> (see <see cref="HotkeyGesture"/>).</summary>
    public string Gesture { get; init; } = DefaultGesture;
}
