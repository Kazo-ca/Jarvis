using Kazo.Jarvis.Kernel.Desktop;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="IGlobalHotkey"/>: registration always succeeds, and a button of the page calls
/// <see cref="Press"/> in place of the keyboard (ADR 0003, section 3).
/// </summary>
public sealed class SimulatedGlobalHotkey : IGlobalHotkey
{
    private readonly Lock gate = new();
    private Action? pressed;

    /// <inheritdoc />
    public HotkeyGesture? Current { get; private set; }

    /// <inheritdoc />
    public HotkeyRegistration Register(HotkeyGesture gesture, Action pressed)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        ArgumentNullException.ThrowIfNull(pressed);
        lock (gate)
        {
            Current = gesture;
            this.pressed = pressed;
        }

        return HotkeyRegistration.Registered;
    }

    /// <inheritdoc />
    public void Unregister()
    {
        lock (gate)
        {
            Current = null;
            pressed = null;
        }
    }

    /// <summary>Simulates a press of the registered hotkey.</summary>
    /// <returns><see langword="false"/> when no hotkey is registered.</returns>
    public bool Press()
    {
        Action? callback;
        lock (gate)
        {
            callback = pressed;
        }

        callback?.Invoke();
        return callback is not null;
    }
}
