namespace Kazo.Jarvis.Kernel.Desktop;

/// <summary>
/// The surface the task fan is drawn on (ADR 0010, section 5). The Windows host keeps a transparent, borderless,
/// topmost window over the screen of the orb, created once and kept hidden (spike E11); the Web host draws the
/// fan as a layer over its page. The fan slice draws the panels; the surface only appears and disappears.
/// Callable from any thread.
/// </summary>
public interface ITaskFanSurface
{
    /// <summary>
    /// Where the centre of the orb is on the surface, read when the fan opens: the fan opens toward the centre of
    /// the screen from there (ADR 0010, section 1).
    /// </summary>
    FanAnchor Anchor { get; }

    /// <summary>Shows the surface, with the keyboard focus so that Escape puts the fan away. The orb stays above it.</summary>
    void Show();

    /// <summary>Hides the surface: closed, the fan draws nothing and costs nothing.</summary>
    void Hide();
}

/// <summary>The centre of the orb on the fan surface, as fractions of its width and of its height.</summary>
/// <param name="X">From 0 (left edge) to 1 (right edge).</param>
/// <param name="Y">From 0 (top edge) to 1 (bottom edge).</param>
public readonly record struct FanAnchor(double X, double Y)
{
    /// <summary>The orb in the bottom right corner, where it sits by default.</summary>
    public static FanAnchor BottomRight { get; } = new(0.94, 0.9);
}
