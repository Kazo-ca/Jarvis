using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Spikes.TaskFanWindow;

/// <summary>
/// Spike E11 (ADR 0010, section 7). Shows a 160x160 transparent topmost orb in the bottom-right corner of the
/// primary screen, a plain window standing for "the application below", and a large transparent fan window.
/// Everything is driven by real mouse input (SetCursorPos + mouse_event), no human needed:
/// warm-up, fan closed (CPU), N open/close cycles by double-click on the orb (CPU, frames, first image),
/// fan open and still (CPU), click routing, single-click delay. Writes results.txt next to the exe, then exits.
/// Arguments: --render wpf|webview --mode recreate|hidden --cycles N --seconds N
/// </summary>
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        string Arg(string name, string fallback) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var runner = new Runner(Arg("--render", "wpf"), Arg("--mode", "hidden"), int.Parse(Arg("--cycles", "8")), int.Parse(Arg("--seconds", "15")));
        app.Startup += async (_, _) =>
        {
            try { await runner.RunAsync(); }
            catch (Exception ex) { runner.Log("ERROR " + ex); }
            finally { runner.Restore(); app.Shutdown(); }
        };
        app.Run();
    }
}

sealed class Runner(string render, string mode, int cycles, int seconds)
{
    readonly string _resultsFile = System.IO.Path.Combine(AppContext.BaseDirectory, "results.txt");
    readonly Native.POINT _cursorAtStart = Native.Cursor();
    readonly uint _doubleClickMs = Native.GetDoubleClickTime();
    readonly List<string> _hits = [];
    readonly List<double> _singleClickDelays = [];
    readonly Stopwatch _clock = Stopwatch.StartNew();
    Window _orb = null!, _target = null!;
    FanWindow? _fan;
    bool _fanOpen, _busy;
    int _activations;
    DispatcherTimer? _pendingSingle;
    double _singleDownAt;
    // Per-animation measurements.
    double _triggerAt, _firstImageAt, _animStart, _animMs;
    readonly List<TimeSpan> _frames = [];
    bool _countFrames;

    public void Log(string line)
    {
        File.AppendAllText(_resultsFile, $"{DateTime.Now:HH:mm:ss} render={render} mode={mode} {line}{Environment.NewLine}");
    }

    public void Restore() => Native.MoveTo(_cursorAtStart.X, _cursorAtStart.Y);

    Rect WorkArea => SystemParameters.WorkArea;

    public async Task RunAsync()
    {
        Log($"start work_area={WorkArea} double_click_time={_doubleClickMs}ms");
        _target = BuildTarget();
        _target.Show();
        _orb = BuildOrb();
        _orb.Show();
        await Task.Delay(3000);
        var baseline = await MeasureCpu(seconds);
        Log($"phase=baseline_orb_only seconds={seconds} cpu_one_core={baseline.Percent:F2}% [{baseline.Detail}]");

        // Warm-up: one unmeasured open/close (JIT, first WebView2 start). In "hidden" mode the fan window stays alive afterwards.
        await DoubleClickOrb(); await WaitFan(true); await Task.Delay(800);
        await DoubleClickOrb(); await WaitFan(false); await Task.Delay(3000);

        // 1) Fan closed.
        var closed = await MeasureCpu(seconds);
        Log($"phase=closed seconds={seconds} cpu_one_core={closed.Percent:F2}% [{closed.Detail}]");

        // 2) Open/close cycles driven by real double-clicks on the orb.
        var openDelays = new List<double>(); var openCpu = new List<double>(); var closeCpu = new List<double>();
        var openFrames = new List<string>(); var closeFrames = new List<string>(); var cycleCpu = new List<double>();
        var singlesBefore = _singleClickDelays.Count;
        for (var c = 0; c < cycles; c++)
        {
            var pids = Cpu.Snapshot().Keys.ToList();
            var cycleStart = Cpu.Total(pids); var cycleWall = Stopwatch.StartNew();
            var (cpuOpen, framesOpen, firstImage) = await Animate(true, pids);
            openCpu.Add(cpuOpen); openFrames.Add(framesOpen); openDelays.Add(firstImage);
            await Task.Delay(1200);
            var (cpuClose, framesClose, _) = await Animate(false, Cpu.Snapshot().Keys.ToList());
            closeCpu.Add(cpuClose); closeFrames.Add(framesClose);
            await Task.Delay(1200);
            cycleCpu.Add(100 * (Cpu.Total(pids) - cycleStart).TotalMilliseconds / cycleWall.Elapsed.TotalMilliseconds);
        }
        Log($"phase=open_anim n={cycles} cpu_one_core_mean={openCpu.Average():F1}% min={openCpu.Min():F1} max={openCpu.Max():F1} frames=[{string.Join(" ", openFrames)}]");
        Log($"phase=close_anim n={cycles} cpu_one_core_mean={closeCpu.Average():F1}% min={closeCpu.Min():F1} max={closeCpu.Max():F1} frames=[{string.Join(" ", closeFrames)}]");
        Log($"phase=first_image n={cycles} mean={openDelays.Average():F0}ms min={openDelays.Min():F0} max={openDelays.Max():F0} values=[{string.Join(" ", openDelays.Select(d => d.ToString("F0")))}] (from the second injected mouse-down)");
        Log($"phase=whole_cycle cpu_one_core_mean={cycleCpu.Average():F1}% (open anim + 1.2 s open + close anim + 1.2 s closed)");
        Log($"check=double_click_never_single singles_during_cycles={_singleClickDelays.Count - singlesBefore}");

        // 3) Fan open and still.
        await DoubleClickOrb(); await WaitFan(true); await Task.Delay(2000);
        var open = await MeasureCpu(seconds);
        Log($"phase=open_still seconds={seconds} cpu_one_core={open.Percent:F2}% [{open.Detail}]");

        // 4) Click routing while open.
        await ClickAndExpect("panel_while_open", ToScreen(_fan!, _fan!.PanelCentre(1)), "panel1");
        await ClickAndExpect("amber_panel_while_open", ToScreen(_fan!, _fan!.PanelCentre(3)), "panel3");
        _singleClickDelays.Clear();
        await ClickAndExpect("orb_single_while_open", OrbCentreOnScreen(), "orb_single", waitMs: (int)_doubleClickMs + 400);
        Log($"check=fan_still_open_after_orb_single open={_fanOpen}");
        await ClickAndExpect("target_area_while_open", TargetCentreOnScreen(), "dim");
        await WaitFan(false); // a click on the darkened desktop closes the fan (ADR 0010, section 3)
        Log($"check=dim_click_closed_fan open={_fanOpen} fan_activations={_activations}");

        // 5) Fan closed: the click must reach the application below, at the same place.
        await Task.Delay(800);
        await ClickAndExpect("target_area_while_closed", TargetCentreOnScreen(), "target");

        // 6) Single click on the orb with the fan closed: delayed by the double-click time.
        _singleClickDelays.Clear();
        for (var i = 0; i < 3; i++) { Native.Click((int)OrbCentreOnScreen().X, (int)OrbCentreOnScreen().Y); await Task.Delay((int)_doubleClickMs + 500); }
        Log($"phase=single_click n={_singleClickDelays.Count} delay_mean={(_singleClickDelays.Count > 0 ? _singleClickDelays.Average() : -1):F0}ms values=[{string.Join(" ", _singleClickDelays.Select(d => d.ToString("F0")))}] double_click_time={_doubleClickMs}ms");

        _fan?.Close(); _orb.Close(); _target.Close();
        Log("end");
    }

    // ---------- Fan control ----------

    async Task OpenFan()
    {
        if (_fan is null)
        {
            _fan = new FanWindow(render, WorkArea, OrbCentreInDips());
            _fan.Hit += i =>
            {
                _hits.Add(i < 0 ? "dim" : "panel" + i);
                if (i < 0 && _fanOpen && !_busy) _ = ToggleFan();
            };
            // Clicking a panel activates the fan window, which then moves above the orb in the topmost band:
            // put the orb back on top at every activation.
            _fan.Activated += (_, _) => { _activations++; Native.BringToTopmost(new WindowInteropHelper(_orb).Handle); };
            _fan.WebFirstFrame += () => { if (_firstImageAt == 0) _firstImageAt = _clock.Elapsed.TotalMilliseconds; };
            _fan.Show();
            if (_fan.IsWeb) await _fan.WebReady;
        }
        else _fan.Show();
        _animStart = _clock.Elapsed.TotalMilliseconds;
        Native.BringToTopmost(new WindowInteropHelper(_orb).Handle); // the orb stays above the fan
        if (_fan.IsWeb) await _fan.AnimateWeb(true); else await _fan.AnimateWpf(true);
        _animMs = _clock.Elapsed.TotalMilliseconds - _animStart;
        if (!_fan.IsWeb) _fan.FreezeWpf(true); // still image while open: no clock left running
    }

    async Task CloseFan()
    {
        _animStart = _clock.Elapsed.TotalMilliseconds;
        if (_fan!.IsWeb) await _fan.AnimateWeb(false); else await _fan.AnimateWpf(false);
        _animMs = _clock.Elapsed.TotalMilliseconds - _animStart;
        if (!_fan.IsWeb) _fan.FreezeWpf(false);
        if (mode == "recreate") { _fan.Close(); _fan = null; } else _fan.Hide();
    }

    async Task ToggleFan()
    {
        if (_busy) return;
        _busy = true;
        try { if (_fanOpen) { await CloseFan(); _fanOpen = false; } else { await OpenFan(); _fanOpen = true; } }
        finally { _busy = false; }
    }

    async Task WaitFan(bool open)
    {
        for (var i = 0; i < 1000 && (_fanOpen != open || _busy); i++) await Task.Delay(20);
        if (_fanOpen != open) throw new InvalidOperationException($"fan did not become {(open ? "open" : "closed")} within 20 s");
    }

    async Task<(double Cpu, string Frames, double FirstImageMs)> Animate(bool open, List<int> pids)
    {
        _frames.Clear(); _firstImageAt = 0; _animStart = 0; _animMs = 0;
        var before = Cpu.Total(pids); var wall = Stopwatch.StartNew();
        _countFrames = true;
        CompositionTarget.Rendering += OnRendering; // only while measuring: a handler keeps WPF rendering every frame
        await DoubleClickOrb();
        await WaitFan(open);
        CompositionTarget.Rendering -= OnRendering;
        _countFrames = false;
        var after = Cpu.Total(Cpu.Snapshot().Keys.Intersect(pids).ToList().Count == pids.Count ? pids : Cpu.Snapshot().Keys.ToList());
        var ms = wall.Elapsed.TotalMilliseconds;
        var distinct = _frames.Distinct().ToList();
        var gaps = distinct.Zip(distinct.Skip(1), (a, b) => (b - a).TotalMilliseconds).ToList();
        var frames = $"{distinct.Count}f/{_animMs:F0}ms(maxgap{(gaps.Count > 0 ? gaps.Max() : 0):F0})";
        return (100 * (after - before).TotalMilliseconds / ms, frames, _firstImageAt > 0 ? _firstImageAt - _triggerAt : -1);
    }

    void OnRendering(object? sender, EventArgs e)
    {
        if (!_countFrames || _animStart == 0 || _animMs > 0) return;
        _frames.Add(((RenderingEventArgs)e).RenderingTime);
        // First WPF frame after the fan window is visible and animating = first image (WPF rendering).
        if (_firstImageAt == 0 && _fan is { IsVisible: true, IsWeb: false } && _busy) _firstImageAt = _clock.Elapsed.TotalMilliseconds;
    }

    // ---------- Input ----------

    async Task DoubleClickOrb()
    {
        var p = OrbCentreOnScreen();
        Native.Click((int)p.X, (int)p.Y);
        await Task.Delay(60);
        _triggerAt = _clock.Elapsed.TotalMilliseconds;
        Native.Click((int)p.X, (int)p.Y);
        await Task.Delay(30);
    }

    async Task ClickAndExpect(string name, Point screen, string expected, int waitMs = 400)
    {
        _hits.Clear();
        Native.Click((int)screen.X, (int)screen.Y);
        await Task.Delay(waitMs);
        Log($"click={name} at=({screen.X:F0},{screen.Y:F0}) expected={expected} got=[{string.Join(",", _hits)}] {(_hits.Contains(expected) && _hits.Count == 1 ? "OK" : "KO")}");
    }

    // ---------- Windows ----------

    Point OrbCentreInDips() => new(WorkArea.Right - 200 + 80, WorkArea.Bottom - 200 + 80);
    Point OrbCentreOnScreen() => _orb.PointToScreen(new Point(80, 80));
    Point TargetCentreOnScreen() => _target.PointToScreen(new Point(180, 110));
    static Point ToScreen(Visual v, Point p) => v.PointToScreen(p);

    Window BuildOrb()
    {
        var orb = new Window
        {
            Title = "E11 orb", Width = 160, Height = 160, WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = Brushes.Transparent, Topmost = true, ShowInTaskbar = false, ShowActivated = false, ResizeMode = ResizeMode.NoResize,
            Left = WorkArea.Right - 200, Top = WorkArea.Bottom - 200,
            Content = new Ellipse
            {
                Width = 120, Height = 120,
                Fill = new RadialGradientBrush(Color.FromArgb(230, 140, 230, 255), Color.FromArgb(60, 0, 90, 200)),
                Stroke = new SolidColorBrush(Color.FromArgb(200, 77, 225, 255)), StrokeThickness = 2,
            },
        };
        // ADR 0010, section 3: a double-click toggles the fan; a single click is handled only once the system
        // double-click time has passed without a second click.
        orb.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount >= 2)
            {
                _pendingSingle?.Stop(); _pendingSingle = null;
                _hits.Add("orb_double");
                _ = ToggleFan();
                return;
            }
            _singleDownAt = _clock.Elapsed.TotalMilliseconds;
            _pendingSingle?.Stop();
            _pendingSingle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_doubleClickMs) };
            _pendingSingle.Tick += (_, _) =>
            {
                _pendingSingle?.Stop(); _pendingSingle = null;
                _singleClickDelays.Add(_clock.Elapsed.TotalMilliseconds - _singleDownAt);
                _hits.Add("orb_single");
            };
            _pendingSingle.Start();
        };
        return orb;
    }

    Window BuildTarget()
    {
        var target = new Window
        {
            Title = "E11 application below", Width = 360, Height = 220, ShowActivated = false, ShowInTaskbar = false,
            Left = WorkArea.Left + 80, Top = WorkArea.Top + 80, ResizeMode = ResizeMode.NoResize,
            Content = new TextBlock { Text = "E11 : application dessous", Margin = new Thickness(20), FontSize = 18 },
        };
        target.MouseLeftButtonDown += (_, _) => _hits.Add("target");
        return target;
    }

    static async Task<(double Percent, string Detail)> MeasureCpu(int seconds)
    {
        var before = Cpu.Snapshot(); var sw = Stopwatch.StartNew();
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        var after = Cpu.Snapshot();
        return Cpu.Diff(before, after, sw.Elapsed.TotalMilliseconds);
    }
}
