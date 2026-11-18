using System.Diagnostics;
using System.IO;
using System.Management;
using System.Windows;
using Microsoft.Web.WebView2.Wpf;

namespace Spikes.OrbWindow;

/// <summary>
/// Spike E6: a transparent, topmost WPF window that shows a Canvas orb through WebView2 (composition control,
/// the only WebView2 mode that keeps per-pixel transparency in a layered window, as found in Kazo Bar).
/// Arguments: --fps N --state idle|speaking --seconds N --mode webview|none. Measures the CPU of this process
/// plus its WebView2 child processes and writes one line to results.txt, then exits.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RunAsync();
    }

    async Task RunAsync()
    {
        var args = Environment.GetCommandLineArgs();
        string Arg(string name, string fallback) { var i = Array.IndexOf(args, name); return i > 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        var fps = Arg("--fps", "30"); var state = Arg("--state", "idle"); var mode = Arg("--mode", "webview");
        var seconds = int.Parse(Arg("--seconds", "30"));

        if (mode == "webview")
        {
            var web = new WebView2CompositionControl { DefaultBackgroundColor = System.Drawing.Color.Transparent };
            Root.Children.Add(web);
            await web.EnsureCoreWebView2Async();
            var root = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("orb.local", root, Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
            web.CoreWebView2.Navigate($"https://orb.local/index.html?fps={fps}&state={state}");
        }

        else if (mode == "wpf") StartNativeOrb(int.Parse(fps));
        else if (mode == "wpfanim") StartAnimatedOrb(int.Parse(fps));

        await Task.Delay(TimeSpan.FromSeconds(8)); // let WebView2 start all its processes and settle
        var before = Snapshot();
        var sw = Stopwatch.StartNew();
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        var after = Snapshot();
        var wall = sw.Elapsed.TotalMilliseconds;

        // CPU of processes alive at both snapshots, as a share of ONE core.
        double total = 0; var detail = new List<string>();
        foreach (var (pid, (name, cpu)) in after)
            if (before.TryGetValue(pid, out var b))
            {
                var pct = 100 * (cpu - b.Cpu).TotalMilliseconds / wall;
                total += pct; detail.Add($"{name}:{pct:F2}");
            }
        var line = $"{DateTime.Now:HH:mm:ss} mode={mode} state={state} fps={fps} seconds={seconds} cpu_one_core={total:F2}% processes={after.Count} [{string.Join(" ", detail)}]";
        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "results.txt"), line + Environment.NewLine);
        Application.Current.Shutdown();
    }

    /// <summary>Orb built once from WPF shapes; only transforms and opacity are animated by WPF's own
    /// animation clock (no per-frame allocation, frame rate capped by Timeline.DesiredFrameRate).</summary>
    void StartAnimatedOrb(int fps)
    {
        var canvas = new System.Windows.Controls.Canvas { Width = 240, Height = 240 };
        Root.Children.Add(canvas);
        var glow = new System.Windows.Shapes.Ellipse
        {
            Width = 140, Height = 140,
            Fill = new System.Windows.Media.RadialGradientBrush(System.Windows.Media.Color.FromArgb(200, 120, 220, 255), System.Windows.Media.Color.FromArgb(0, 0, 120, 255)),
        };
        System.Windows.Controls.Canvas.SetLeft(glow, 50); System.Windows.Controls.Canvas.SetTop(glow, 50);
        canvas.Children.Add(glow);
        var breath = new System.Windows.Media.Animation.DoubleAnimation(0.55, 0.95, TimeSpan.FromSeconds(5)) { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
        System.Windows.Media.Animation.Timeline.SetDesiredFrameRate(breath, fps);
        glow.BeginAnimation(OpacityProperty, breath);
        for (var i = 0; i < 3; i++)
        {
            var r = 50 + i * 18.0;
            var p1 = new Point(120 + r * Math.Cos(Math.PI * 1.4), 120 + r * Math.Sin(Math.PI * 1.4));
            var fig = new System.Windows.Media.PathFigure(new Point(120 + r, 120), [new System.Windows.Media.ArcSegment(p1, new Size(r, r), 0, true, System.Windows.Media.SweepDirection.Clockwise, true)], false);
            var rot = new System.Windows.Media.RotateTransform(0, 120, 120);
            var path = new System.Windows.Shapes.Path
            {
                Data = new System.Windows.Media.PathGeometry([fig]), StrokeThickness = 2, RenderTransform = rot,
                Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(160 - i * 30), 140, 230, 255)),
            };
            canvas.Children.Add(path);
            var spin = new System.Windows.Media.Animation.DoubleAnimation(0, i % 2 == 1 ? -360 : 360, TimeSpan.FromSeconds(15 + i * 6)) { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
            System.Windows.Media.Animation.Timeline.SetDesiredFrameRate(spin, fps);
            rot.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, spin);
        }
    }

    /// <summary>Same orb drawn by WPF itself (no browser): three arcs and a glow, redrawn at the given rate.</summary>
    void StartNativeOrb(int fps)
    {
        var visual = new System.Windows.Controls.Image { Width = 240, Height = 240 };
        Root.Children.Add(visual);
        var start = Stopwatch.StartNew();
        var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render)
        { Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, fps)) };
        timer.Tick += (_, _) => visual.Source = DrawOrb(start.Elapsed.TotalMilliseconds);
        if (fps > 0) timer.Start(); else visual.Source = DrawOrb(0);
    }

    static System.Windows.Media.DrawingImage DrawOrb(double t)
    {
        var g = new System.Windows.Media.DrawingGroup();
        using (var dc = g.Open())
        {
            var breath = 0.5 + 0.5 * Math.Sin(t / 1600);
            dc.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, new Rect(0, 0, 240, 240));
            var glow = new System.Windows.Media.RadialGradientBrush(
                System.Windows.Media.Color.FromArgb((byte)(140 + 70 * breath), 120, 220, 255), System.Windows.Media.Color.FromArgb(0, 0, 120, 255));
            dc.DrawEllipse(glow, null, new Point(120, 120), 70, 70);
            for (var i = 0; i < 3; i++)
            {
                var r = 48 + i * 18 + 4 * breath;
                var a0 = t / (2400 + i * 900) * (i % 2 == 1 ? -1 : 1);
                var pen = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(255 * (0.35 + 0.2 * breath - i * 0.08)), 140, 230, 255)), 2);
                var p0 = new Point(120 + r * Math.Cos(a0), 120 + r * Math.Sin(a0));
                var p1 = new Point(120 + r * Math.Cos(a0 + Math.PI * 1.4), 120 + r * Math.Sin(a0 + Math.PI * 1.4));
                var fig = new System.Windows.Media.PathFigure(p0, [new System.Windows.Media.ArcSegment(p1, new Size(r, r), 0, true, System.Windows.Media.SweepDirection.Clockwise, true)], false);
                dc.DrawGeometry(null, pen, new System.Windows.Media.PathGeometry([fig]));
            }
        }
        var img = new System.Windows.Media.DrawingImage(g); img.Freeze(); return img;
    }

    /// <summary>This process and every descendant (WebView2 browser, GPU, renderer, utility processes).</summary>
    static Dictionary<int, (string Name, TimeSpan Cpu)> Snapshot()
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
}
