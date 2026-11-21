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
    private const int WtsCurrentSession = -1;
    private const int WtsSessionInfoEx = 25;
    private const int WtsSessionStateLock = 0;
    private const int WtsSessionStateUnlock = 1;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    /// <summary>Adds extended styles to a window (no activation, no Alt+Tab entry for the orb).</summary>
    public static void AddExtendedStyle(IntPtr window, int style)
    {
        var current = GetWindowLongPtr(window, GwlExStyle).ToInt64();
        SetWindowLongPtr(window, GwlExStyle, new IntPtr(current | (long)(uint)style));
    }

    /// <summary>Removes extended styles from a window (the browser of Jarvis, once the user opens it).</summary>
    public static void RemoveExtendedStyle(IntPtr window, int style)
    {
        var current = GetWindowLongPtr(window, GwlExStyle).ToInt64();
        SetWindowLongPtr(window, GwlExStyle, new IntPtr(current & ~(long)(uint)style));
    }

    /// <summary>The size of the client area of a window, in physical pixels.</summary>
    public static (int Width, int Height) ClientSize(IntPtr window) =>
        GetClientRect(window, out var rect) ? (rect.Right - rect.Left, rect.Bottom - rect.Top) : (0, 0);

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

    /// <summary>The window's top-left corner in physical screen pixels.</summary>
    public static (int X, int Y) WindowPosition(IntPtr window) =>
        GetWindowRect(window, out var rect) ? (rect.Left, rect.Top) : (0, 0);

    /// <summary>Moves a window to physical screen pixels, without resizing, reordering or activating it.</summary>
    public static void MoveWindow(IntPtr window, int x, int y) =>
        SetWindowPos(window, IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);

    /// <summary>
    /// Whether the current session is locked, read from <c>WTSSessionInfoEx</c>; <see langword="null"/> when
    /// Windows does not say. On Windows 10 and 11 the lock screen runs on the <c>Default</c> desktop, so the
    /// input desktop name alone does not reveal a lock (Windows check of work item stage1/4-hosts).
    /// </summary>
    public static bool? IsSessionLocked()
    {
        if (!WTSQuerySessionInformation(IntPtr.Zero, WtsCurrentSession, WtsSessionInfoEx, out var buffer, out var bytes))
        {
            return null;
        }

        try
        {
            // WTSINFOEXW: DWORD Level, then the WTSINFOEX_LEVEL1_W union aligned on 8 bytes (it holds
            // LARGE_INTEGER fields): SessionId at 8, SessionState at 12, SessionFlags at 16.
            if (bytes < 20 || Marshal.ReadInt32(buffer, 0) != 1)
            {
                return null;
            }

            return Marshal.ReadInt32(buffer, 16) switch
            {
                WtsSessionStateLock => true,
                WtsSessionStateUnlock => false,
                _ => null,
            };
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    private static extern bool WTSQuerySessionInformation(IntPtr server, int sessionId, int infoClass, out IntPtr buffer, out int bytesReturned);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
