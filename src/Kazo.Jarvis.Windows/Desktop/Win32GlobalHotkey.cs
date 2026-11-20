using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using Kazo.Jarvis.Kernel.Desktop;

namespace Kazo.Jarvis.Windows.Desktop;

/// <summary>
/// <see cref="IGlobalHotkey"/> of the Windows host: <c>RegisterHotKey</c> on a message-only window of the UI
/// thread, so the hotkey works while another application has the focus (US1.6).
/// </summary>
internal sealed class Win32GlobalHotkey : IGlobalHotkey, IDisposable
{
    private const int HotkeyId = 0x4A41;
    private static readonly IntPtr MessageOnlyParent = new(-3);

    private readonly Dispatcher dispatcher;
    private HwndSource? source;
    private Action? pressed;

    /// <summary>Creates the service. The message window is created at the first registration.</summary>
    public Win32GlobalHotkey(Dispatcher dispatcher) => this.dispatcher = dispatcher;

    /// <inheritdoc />
    public HotkeyGesture? Current { get; private set; }

    /// <inheritdoc />
    public HotkeyRegistration Register(HotkeyGesture gesture, Action pressed)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        ArgumentNullException.ThrowIfNull(pressed);
        if (dispatcher.HasShutdownStarted)
        {
            return HotkeyRegistration.Failed;
        }

        return dispatcher.Invoke(() =>
        {
            UnregisterOnUiThread();
            var key = VirtualKey(gesture.Key);
            if (key == 0)
            {
                return HotkeyRegistration.Failed;
            }

            source ??= CreateSource();
            if (!NativeMethods.RegisterHotKey(source.Handle, HotkeyId, Modifiers(gesture.Modifiers) | NativeMethods.ModNoRepeat, key))
            {
                return Marshal.GetLastPInvokeError() == NativeMethods.ErrorHotkeyAlreadyRegistered
                    ? HotkeyRegistration.AlreadyTaken
                    : HotkeyRegistration.Failed;
            }

            Current = gesture;
            this.pressed = pressed;
            return HotkeyRegistration.Registered;
        });
    }

    /// <inheritdoc />
    public void Unregister()
    {
        if (!dispatcher.HasShutdownStarted)
        {
            dispatcher.Invoke(UnregisterOnUiThread);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Unregister();
        source?.Dispose();
    }

    /// <summary>The virtual-key code of a canonical key name of <see cref="HotkeyGesture"/>; 0 when unknown.</summary>
    internal static uint VirtualKey(string key) => key switch
    {
        // Letters and digits share their ASCII code with their virtual-key code.
        [var single] when char.IsAsciiLetterUpper(single) || char.IsAsciiDigit(single) => single,
        ['F', ..] when int.TryParse(key.AsSpan(1), out var number) && number is >= 1 and <= 24 => (uint)(0x70 + number - 1),
        "Space" => 0x20,
        "PageUp" => 0x21,
        "PageDown" => 0x22,
        "End" => 0x23,
        "Home" => 0x24,
        "Insert" => 0x2D,
        "Delete" => 0x2E,
        "Pause" => 0x13,
        _ => 0,
    };

    private void UnregisterOnUiThread()
    {
        if (Current is not null && source is not null)
        {
            NativeMethods.UnregisterHotKey(source.Handle, HotkeyId);
        }

        Current = null;
        pressed = null;
    }

    private HwndSource CreateSource()
    {
        var created = new HwndSource(new HwndSourceParameters("Kazo.Jarvis.Hotkey") { ParentWindow = MessageOnlyParent, WindowStyle = 0 });
        created.AddHook(OnMessage);
        return created;
    }

    private IntPtr OnMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            pressed?.Invoke();
        }

        return IntPtr.Zero;
    }

    private static uint Modifiers(HotkeyModifiers modifiers) =>
        (modifiers.HasFlag(HotkeyModifiers.Control) ? NativeMethods.ModControl : 0)
        | (modifiers.HasFlag(HotkeyModifiers.Alt) ? NativeMethods.ModAlt : 0)
        | (modifiers.HasFlag(HotkeyModifiers.Shift) ? NativeMethods.ModShift : 0)
        | (modifiers.HasFlag(HotkeyModifiers.Windows) ? NativeMethods.ModWin : 0);
}
