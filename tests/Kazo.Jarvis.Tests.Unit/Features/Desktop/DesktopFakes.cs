using Kazo.Jarvis.Kernel.Desktop;

namespace Kazo.Jarvis.Tests.Unit.Features.Desktop;

/// <summary>A global hotkey that records registrations and can refuse them.</summary>
internal sealed class FakeGlobalHotkey : IGlobalHotkey
{
    public List<HotkeyGesture> Registered { get; } = [];

    public HotkeyRegistration NextResult { get; set; } = HotkeyRegistration.Registered;

    public int Unregistrations { get; private set; }

    public HotkeyGesture? Current { get; private set; }

    private Action? Pressed { get; set; }

    public HotkeyRegistration Register(HotkeyGesture gesture, Action pressed)
    {
        Registered.Add(gesture);
        Current = NextResult == HotkeyRegistration.Registered ? gesture : null;
        Pressed = NextResult == HotkeyRegistration.Registered ? pressed : null;
        return NextResult;
    }

    public void Unregister()
    {
        Unregistrations++;
        Current = null;
        Pressed = null;
    }

    public void Press() => Pressed?.Invoke();
}

/// <summary>A start at sign-in that records what it was asked.</summary>
internal sealed class FakeLaunchAtLogin : ILaunchAtLogin
{
    public List<bool> Requests { get; } = [];

    public bool Throws { get; set; }

    public bool IsEnabled { get; private set; }

    public bool SetEnabled(bool enabled)
    {
        if (Throws)
        {
            throw new UnauthorizedAccessException("Registry refused.");
        }

        Requests.Add(enabled);
        IsEnabled = enabled;
        return true;
    }
}
