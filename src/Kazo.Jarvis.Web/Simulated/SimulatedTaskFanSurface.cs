using Kazo.Jarvis.Kernel.Desktop;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="ITaskFanSurface"/> and <see cref="IPointerTimings"/>: the Web host draws the fan as a
/// layer over its page (ADR 0010, section 4), and its orb sits in one of the four corners of the page, which a
/// button of the test page changes.
/// </summary>
public sealed class SimulatedTaskFanSurface : ITaskFanSurface, IPointerTimings
{
    // The centre of the orb in each corner; Home.razor.css places the orb at the same fractions.
    private static readonly (string Name, FanAnchor Anchor)[] Corners =
    [
        ("bottom-right", new FanAnchor(0.92, 0.86)),
        ("bottom-left", new FanAnchor(0.08, 0.86)),
        ("top-left", new FanAnchor(0.08, 0.14)),
        ("top-right", new FanAnchor(0.92, 0.14)),
    ];

    private volatile int corner;
    private volatile bool shown;

    /// <summary>Raised after the surface was shown or hidden, or the orb moved, on any thread.</summary>
    public event EventHandler? Changed;

    /// <summary>The corner the orb is in: <c>bottom-right</c> at first.</summary>
    public string Corner => Corners[corner].Name;

    /// <summary>Whether the surface is shown.</summary>
    public bool IsShown => shown;

    /// <inheritdoc />
    public FanAnchor Anchor => Corners[corner].Anchor;

    /// <summary>Shorter than the half second of Windows, so that the scenarios do not wait; long enough for a real double click.</summary>
    public TimeSpan DoubleClickTime { get; } = TimeSpan.FromMilliseconds(300);

    /// <inheritdoc />
    public void Show() => Set(true);

    /// <inheritdoc />
    public void Hide() => Set(false);

    /// <summary>Moves the orb to the next corner, as the user drags it on Windows.</summary>
    public void NextCorner()
    {
        corner = (corner + 1) % Corners.Length;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Set(bool value)
    {
        shown = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
