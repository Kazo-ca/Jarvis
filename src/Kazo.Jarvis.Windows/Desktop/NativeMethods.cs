using System.Runtime.InteropServices;

namespace Kazo.Jarvis.Windows.Desktop;

/// <summary>Win32 calls of the Windows host.</summary>
internal static class NativeMethods
{
    public const int GwlExStyle = -20;
    public const int WsExToolWindow = 0x00000080;
    public const int WsExNoActivate = 0x08000000;
    public const int WmHotkey = 0x0312;
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;
    public const int ErrorHotkeyAlreadyRegistered = 1409;
    private const uint DesktopReadObjects = 0x0001;
    private const int UoiName = 2;

    /// <summary>Adds extended styles to a window (no activation, no Alt+Tab entry for the orb).</summary>
    public static void AddExtendedStyle(IntPtr window, int style)
    {
        var current = GetWindowLongPtr(window, GwlExStyle).ToInt64();
        SetWindowLongPtr(window, GwlExStyle, new IntPtr(current | (long)(uint)style));
    }

    /// <summary>
    /// The name of the desktop that receives the input: <c>Default</c> in a normal session, <c>Winlogon</c>
    /// on the lock screen or <c>Screen-saver</c>; <see langword="null"/> when it cannot be opened (a
    /// disconnected remote session, for instance).
    /// </summary>
    public static string? InputDesktopName()
    {
        var desktop = OpenInputDesktop(0, false, DesktopReadObjects);
        if (desktop == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new char[256];
            return GetUserObjectInformation(desktop, UoiName, buffer, buffer.Length * sizeof(char), out _)
                ? new string(buffer).TrimEnd('\0')
                : null;
        }
        finally
        {
            CloseDesktop(desktop);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint desiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr desktop);

    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetUserObjectInformation(IntPtr handle, int index, [Out] char[] info, int length, out int needed);
}
