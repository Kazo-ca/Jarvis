namespace Kazo.Jarvis.Kernel.Desktop;

/// <summary>Modifier keys of a global hotkey.</summary>
[Flags]
public enum HotkeyModifiers
{
    /// <summary>No modifier.</summary>
    None = 0,

    /// <summary>Control.</summary>
    Control = 1,

    /// <summary>Alt.</summary>
    Alt = 2,

    /// <summary>Shift.</summary>
    Shift = 4,

    /// <summary>The Windows key.</summary>
    Windows = 8,
}
