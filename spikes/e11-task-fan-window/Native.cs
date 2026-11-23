using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace Spikes.TaskFanWindow;

/// <summary>Win32 helpers: real mouse input (so clicks go through Windows hit-testing like a human's),
/// the system double-click time, and z-order.</summary>
static class Native
{
    [DllImport("user32.dll")] public static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    const uint LeftDown = 0x0002, LeftUp = 0x0004;
    static readonly IntPtr HwndTopmost = new(-1);

    public static POINT Cursor() { GetCursorPos(out var p); return p; }
    public static void MoveTo(int x, int y) => SetCursorPos(x, y);

    /// <summary>One left click at a screen point in physical pixels.</summary>
    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(LeftDown, 0, 0, 0, UIntPtr.Zero);
        mouse_event(LeftUp, 0, 0, 0, UIntPtr.Zero);
    }

    /// <summary>Puts a window back at the top of the topmost band without activating it.</summary>
    public static void BringToTopmost(IntPtr hwnd) => SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
}

/// <summary>Same CPU method as spike E6: this process and every descendant (WebView2), as a share of ONE core.</summary>
static class Cpu
{
    public static Dictionary<int, (string Name, TimeSpan Cpu)> Snapshot()
    {
        var parents = new Dictionary<int, int>();
        using (var q = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId FROM Win32_Process"))
            foreach (ManagementObject o in q.Get())
                parents[Convert.ToInt32(o["ProcessId"])] = Convert.ToInt32(o["ParentProcessId"]);
        var self = Environment.ProcessId;
        bool Descends(int pid) { for (var i = 0; i < 16 && pid != 0; i++) { if (pid == self) return true; if (!parents.TryGetValue(pid, out pid)) return false; } return false; }
        var result = new Dictionary<int, (string, TimeSpan)>();
        foreach (var pid in parents.Keys.Where(Descends))
            try { using var p = Process.GetProcessById(pid); result[pid] = (p.ProcessName, p.TotalProcessorTime); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        return result;
    }

    /// <summary>Cheap snapshot for short windows (animations): process ids are taken once, only CPU times are re-read.</summary>
    public static TimeSpan Total(IReadOnlyCollection<int> pids)
    {
        var total = TimeSpan.Zero;
        foreach (var pid in pids)
            try { using var p = Process.GetProcessById(pid); total += p.TotalProcessorTime; }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        return total;
    }

    public static (double Percent, string Detail) Diff(Dictionary<int, (string Name, TimeSpan Cpu)> before, Dictionary<int, (string Name, TimeSpan Cpu)> after, double wallMs)
    {
        double total = 0; var detail = new List<string>();
        foreach (var (pid, (name, cpu)) in after)
            if (before.TryGetValue(pid, out var b))
            {
                var pct = 100 * (cpu - b.Cpu).TotalMilliseconds / wallMs;
                total += pct; detail.Add($"{name}:{pct:F1}");
            }
        return (total, string.Join(" ", detail));
    }
}
