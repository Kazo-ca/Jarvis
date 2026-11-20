namespace Kazo.Jarvis.Windows.Desktop;

/// <summary>
/// Whether the user's desktop can show windows now. On a locked session the input desktop is the lock
/// screen, and Direct3D 9, which <c>WebView2CompositionControl</c> needs, is not available: the host waits
/// instead of dying (pitfall of work item stage1/1-solution). <c>LogonUI.exe</c> is no sign: it also runs in
/// an unlocked remote session.
/// </summary>
internal static class SessionDesktop
{
    /// <summary>The name of the input desktop of an unlocked session.</summary>
    public const string Unlocked = "Default";

    /// <summary>Whether the input desktop is the user's desktop.</summary>
    /// <param name="name">The input desktop name read, or <see langword="null"/> when it could not be read.</param>
    /// <returns><see langword="true"/> when windows can be shown.</returns>
    public static bool IsReady(out string? name)
    {
        name = NativeMethods.InputDesktopName();
        return string.Equals(name, Unlocked, StringComparison.OrdinalIgnoreCase);
    }
}
