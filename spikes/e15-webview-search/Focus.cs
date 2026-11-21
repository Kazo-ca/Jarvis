using System.Diagnostics;

namespace Spikes.WebViewSearch;

/// <summary>One before/after foreground check around an operation.</summary>
internal sealed record FocusCheck(string Op, bool Changed, bool AfterIsOurs, string Before, string After);

/// <summary>A foreground change seen by the 100 ms sampler.</summary>
internal sealed record FocusChange(double AtMs, string Process, bool IsOurs);

internal sealed class FocusViolationException(string op)
    : Exception($"foreground window moved to the spike during '{op}'");

/// <summary>
/// Watches GetForegroundWindow: a sampler every 100 ms plus a before/after check around
/// each guarded operation. "Ours" means the spike process or one of its WebView2 processes.
/// </summary>
internal sealed class FocusWatch : IDisposable
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Timer timer;
    private readonly object gate = new();
    private IntPtr last;
    private HashSet<int> ours = [Environment.ProcessId];

    public List<FocusChange> Changes { get; } = [];
    public List<FocusCheck> Checks { get; } = [];

    public FocusWatch()
    {
        last = Native.GetForegroundWindow();
        timer = new Timer(_ => Sample(), null, 0, 100);
    }

    public void SetOurs(IEnumerable<int> pids)
    {
        lock (gate) ours = [Environment.ProcessId, .. pids];
    }

    private bool IsOurs(IntPtr hwnd)
    {
        var pid = Native.PidOf(hwnd);
        lock (gate) return pid != 0 && ours.Contains(pid);
    }

    private void Sample()
    {
        var h = Native.GetForegroundWindow();
        lock (gate)
        {
            if (h == last) return;
            last = h;
        }
        var change = new FocusChange(Math.Round(clock.Elapsed.TotalMilliseconds), ProcName(h), IsOurs(h));
        lock (gate) Changes.Add(change);
    }

    public static string ProcName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "(none)";
        try
        {
            using var p = Process.GetProcessById(Native.PidOf(hwnd));
            return p.ProcessName;
        }
        catch (ArgumentException)
        {
            return "(exited)";
        }
    }

    public async Task<T> Guard<T>(string op, Func<Task<T>> action)
    {
        var before = Native.GetForegroundWindow();
        var result = await action();
        var after = Native.GetForegroundWindow();
        var check = new FocusCheck(op, before != after, IsOurs(after), ProcName(before), ProcName(after));
        lock (gate) Checks.Add(check);
        if (check.AfterIsOurs) throw new FocusViolationException(op);
        return result;
    }

    public Task Guard(string op, Func<Task> action) => Guard(op, async () => { await action(); return 0; });

    public object Summary()
    {
        lock (gate)
        {
            return new
            {
                checks = Checks.Count,
                checksChanged = Checks.Count(c => c.Changed),
                checksForegroundOurs = Checks.Count(c => c.AfterIsOurs),
                samplerChanges = Changes.Count,
                samplerChangesToOurs = Changes.Count(c => c.IsOurs),
            };
        }
    }

    public void Dispose()
    {
        timer.Dispose();
        if (LocalDetailsFile is not null)
        {
            lock (gate)
            {
                System.IO.File.WriteAllText(LocalDetailsFile, System.Text.Json.JsonSerializer.Serialize(new { Changes, Checks }));
            }
        }
    }

    /// <summary>Local-only file (process names of foreground changes); never committed.</summary>
    public string? LocalDetailsFile { get; set; }
}
