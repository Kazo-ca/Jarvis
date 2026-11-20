namespace Kazo.Jarvis.Windows.Desktop;

/// <summary>
/// Whether the user's desktop can show windows now. On a locked session Direct3D 9, which
/// <c>WebView2CompositionControl</c> needs, may not be available: the host waits instead of dying (pitfall of
/// work item stage1/1-solution). Two signs, both required: the session is not locked
/// (<c>WTSSessionInfoEx</c>, the reliable one on Windows 10 and 11, whose lock screen runs on the
/// <c>Default</c> desktop), and the input desktop is <c>Default</c> (not the secure desktop of a credential
/// prompt, nor missing in a disconnected remote session). <c>LogonUI.exe</c> is no sign: it also runs in an
/// unlocked remote session.
/// </summary>
internal static class SessionDesktop
{
    /// <summary>The name of the input desktop of an unlocked session.</summary>
    public const string Unlocked = "Default";

    /// <summary>Whether windows can be shown.</summary>
    /// <param name="reason">When not ready, what was seen, for the log.</param>
    /// <returns><see langword="true"/> when windows can be shown.</returns>
    public static bool IsReady(out string? reason)
    {
        if (NativeMethods.IsSessionLocked() == true)
        {
            reason = "session locked";
            return false;
        }

        var desktop = NativeMethods.InputDesktopName();
        if (!string.Equals(desktop, Unlocked, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"input desktop {desktop ?? "unavailable"}";
            return false;
        }

        reason = null;
        return true;
    }
}
