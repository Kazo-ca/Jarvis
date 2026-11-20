using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Kazo.Jarvis.Kernel.Desktop;

/// <summary>
/// A global hotkey (US1.6), written as in the settings: modifiers then one key, joined by <c>+</c>, for
/// instance <c>Ctrl+Alt+J</c>. At least one of Ctrl, Alt or Win is required, so that typing in another
/// application never triggers it by accident.
/// </summary>
/// <param name="Modifiers">The modifier keys.</param>
/// <param name="Key">The key, in its canonical name: <c>A</c>-<c>Z</c>, <c>0</c>-<c>9</c>, <c>F1</c>-<c>F24</c> or one of <see cref="NamedKeys"/>.</param>
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, string Key)
{
    /// <summary>Keys allowed besides letters, digits and function keys.</summary>
    public static IReadOnlyList<string> NamedKeys { get; } = ["Space", "Insert", "Delete", "Home", "End", "PageUp", "PageDown", "Pause"];

    private static readonly (string Name, HotkeyModifiers Modifier)[] ModifierNames =
    [
        ("Ctrl", HotkeyModifiers.Control),
        ("Alt", HotkeyModifiers.Alt),
        ("Shift", HotkeyModifiers.Shift),
        ("Win", HotkeyModifiers.Windows),
    ];

    /// <summary>Reads a gesture such as <c>Ctrl+Alt+J</c>. Case and spaces do not matter; <c>Control</c> is accepted for <c>Ctrl</c>.</summary>
    /// <param name="text">The text to read.</param>
    /// <param name="gesture">The gesture, when the text is valid.</param>
    /// <returns><see langword="true"/> when the text is a valid gesture.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out HotkeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        foreach (var part in parts[..^1])
        {
            var modifier = ParseModifier(part);
            if (modifier == HotkeyModifiers.None || modifiers.HasFlag(modifier))
            {
                return false;
            }

            modifiers |= modifier;
        }

        var key = CanonicalKey(parts[^1]);
        if (key is null || (modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Windows)) == 0)
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }

    /// <summary>Reads a gesture, or throws.</summary>
    /// <param name="text">The text to read.</param>
    /// <returns>The gesture.</returns>
    /// <exception cref="FormatException"><paramref name="text"/> is not a valid gesture.</exception>
    public static HotkeyGesture Parse(string text) =>
        TryParse(text, out var gesture) ? gesture : throw new FormatException($"'{text}' is not a valid hotkey.");

    /// <summary>The gesture as written in the settings, modifiers in the order Ctrl, Alt, Shift, Win.</summary>
    /// <returns>For instance <c>Ctrl+Alt+J</c>.</returns>
    public override string ToString() =>
        string.Join('+', ModifierNames.Where(item => Modifiers.HasFlag(item.Modifier)).Select(item => item.Name).Append(Key));

    private static HotkeyModifiers ParseModifier(string text) => text.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => HotkeyModifiers.Control,
        "ALT" => HotkeyModifiers.Alt,
        "SHIFT" => HotkeyModifiers.Shift,
        "WIN" or "WINDOWS" => HotkeyModifiers.Windows,
        _ => HotkeyModifiers.None,
    };

    private static string? CanonicalKey(string text)
    {
        if (text.Length == 1 && char.IsAsciiLetterOrDigit(text[0]))
        {
            return text.ToUpperInvariant();
        }

        if (text.Length is 2 or 3 && (text[0] is 'F' or 'f')
            && int.TryParse(text.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            && number is >= 1 and <= 24 && text[1] != '0')
        {
            return "F" + number.ToString(CultureInfo.InvariantCulture);
        }

        return NamedKeys.FirstOrDefault(name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase));
    }
}
