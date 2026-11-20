namespace Kazo.Jarvis.Kernel.Desktop;

/// <summary>
/// The window that shows the orb (ADR 0003, section 5; US1.1). The Windows host shows a transparent window
/// that remembers its position; the Web host shows the orb in its page. Callable from any thread.
/// </summary>
public interface IOrbWindow
{
    /// <summary>Whether the orb is shown.</summary>
    bool IsVisible { get; }

    /// <summary>Raised after the orb was shown or hidden, on any thread.</summary>
    event EventHandler? VisibilityChanged;

    /// <summary>Shows the orb, without taking the focus. Does nothing when it is already shown.</summary>
    void Show();

    /// <summary>Hides the orb. Does nothing when it is already hidden.</summary>
    void Hide();

    /// <summary>Shows the orb when it is hidden, hides it otherwise (left click on the tray icon, US1.2).</summary>
    void ToggleVisibility();
}
