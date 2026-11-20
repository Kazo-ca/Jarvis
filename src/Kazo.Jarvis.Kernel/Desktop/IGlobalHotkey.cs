namespace Kazo.Jarvis.Kernel.Desktop;

/// <summary>
/// The global hotkey (ADR 0003, section 5; US1.6): works while another application has the focus. One
/// hotkey at a time. The Web host simulates it with a button. Callable from any thread.
/// </summary>
public interface IGlobalHotkey
{
    /// <summary>The active hotkey, or <see langword="null"/>.</summary>
    HotkeyGesture? Current { get; }

    /// <summary>
    /// Replaces the active hotkey with <paramref name="gesture"/>. When it fails, no hotkey stays active.
    /// </summary>
    /// <param name="gesture">The new hotkey.</param>
    /// <param name="pressed">Called on each press, on a thread of the host's choosing; must return quickly.</param>
    /// <returns>Whether the hotkey is active.</returns>
    HotkeyRegistration Register(HotkeyGesture gesture, Action pressed);

    /// <summary>Removes the active hotkey. Does nothing when there is none.</summary>
    void Unregister();
}
