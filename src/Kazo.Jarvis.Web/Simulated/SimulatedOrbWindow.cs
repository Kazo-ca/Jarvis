using Kazo.Jarvis.Kernel.Desktop;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>Simulated <see cref="IOrbWindow"/>: the Web host shows the orb in its page, visible or not.</summary>
public sealed class SimulatedOrbWindow : IOrbWindow
{
    private volatile bool visible = true;

    /// <inheritdoc />
    public bool IsVisible => visible;

    /// <inheritdoc />
    public event EventHandler? VisibilityChanged;

    /// <inheritdoc />
    public void Show() => Set(true);

    /// <inheritdoc />
    public void Hide() => Set(false);

    /// <inheritdoc />
    public void ToggleVisibility() => Set(!visible);

    private void Set(bool value)
    {
        if (visible == value)
        {
            return;
        }

        visible = value;
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }
}
