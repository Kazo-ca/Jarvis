namespace Kazo.Jarvis.Kernel.Desktop;

/// <summary>What happened when a global hotkey was registered.</summary>
public enum HotkeyRegistration
{
    /// <summary>The hotkey is active.</summary>
    Registered,

    /// <summary>Another application already uses this combination; no hotkey is active.</summary>
    AlreadyTaken,

    /// <summary>The host cannot register a global hotkey (no window yet, unsupported key); no hotkey is active.</summary>
    Failed,
}
