using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Web.WebView2.Wpf;

namespace Spikes.TaskFanWindow;

/// <summary>
/// The task fan of ADR 0010: one large transparent, borderless, topmost window over the work area of the orb's
/// screen. It darkens the desktop and shows four panels in an arc toward the screen centre. Two renderings:
/// "wpf" (WPF shapes, WPF animations capped at 30 images per second) and "webview" (the whole fan in one
/// WebView2CompositionControl, HTML panels animated by a 30 fps timer), the product's panels being Razor views.
/// </summary>
sealed class FanWindow : Window
{
    public const int OpenMs = 400, CloseMs = 300;
    readonly string _render;
    readonly Point _orbCentre; // in this window's coordinates (DIPs)
    readonly Canvas _canvas = new();
    readonly Rectangle _dim = new() { Fill = new SolidColorBrush(Color.FromArgb(0x8C, 0x00, 0x08, 0x14)) };
    readonly List<FrameworkElement> _panels = [];
    readonly List<Line> _links = [];
    WebView2CompositionControl? _web;
    TaskCompletionSource<bool>? _webReady;
    TaskCompletionSource<(int Frames, double Ms)>? _webDone;

    /// <summary>Raised on a click that reached a panel (index) or the darkened desktop (-1).</summary>
    public event Action<int>? Hit;
    /// <summary>Raised by the webview page when it drew the first image of an opening.</summary>
    public event Action? WebFirstFrame;

    public FanWindow(string render, Rect workArea, Point orbCentreOnScreen)
    {
        _render = render;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        Left = workArea.Left; Top = workArea.Top; Width = workArea.Width; Height = workArea.Height;
        Title = "E11 fan";
        _orbCentre = new Point(orbCentreOnScreen.X - workArea.Left, orbCentreOnScreen.Y - workArea.Top);
        if (render == "webview") BuildWeb(); else BuildWpf();
    }

    public bool IsWeb => _render == "webview";
    public Point PanelCentre(int i) => _render == "webview" ? WebPanelCentre(i) : _panels[i].TranslatePoint(new Point(_panels[i].Width / 2, _panels[i].Height / 2), this);

    // ---------- WPF rendering ----------

    static readonly string[] Titles = ["Claude Code · Kazo.Jarvis", "Claude Code · Kazo Bar", "Tâche planifiée · météo", "Décision : écrire build.ps1"];

    Point PanelPosition(int i)
    {
        // Arc toward the screen centre: the orb is in the bottom-right corner, panels fan from 180° to 270°.
        var angle = Math.PI * (1.0 + 0.5 * i / 3.0) + 0.12;
        const double radius = 520;
        return new Point(_orbCentre.X + radius * Math.Cos(angle) - 170, _orbCentre.Y + radius * Math.Sin(angle) - 70);
    }

    void BuildWpf()
    {
        var root = new Grid();
        root.Children.Add(_dim);
        root.Children.Add(_canvas);
        Content = root;
        _dim.MouseLeftButtonDown += (_, e) => { Hit?.Invoke(-1); e.Handled = true; };
        for (var i = 0; i < 4; i++)
        {
            var amber = i == 3;
            var tint = amber ? Color.FromRgb(0xFF, 0xB3, 0x47) : Color.FromRgb(0x4D, 0xE1, 0xFF);
            var pos = PanelPosition(i);
            var link = new Line
            {
                X1 = _orbCentre.X, Y1 = _orbCentre.Y, X2 = pos.X + 170, Y2 = pos.Y + 70,
                Stroke = new SolidColorBrush(tint), StrokeThickness = 1.5, StrokeDashArray = [4, 4], Opacity = 0,
                IsHitTestVisible = false,
            };
            _canvas.Children.Add(link); _links.Add(link);
            var text = new StackPanel { Margin = new Thickness(14) };
            text.Children.Add(new TextBlock { Text = Titles[i], Foreground = new SolidColorBrush(tint), FontSize = 16, FontWeight = FontWeights.SemiBold });
            text.Children.Add(new TextBlock { Text = amber ? "En attente d'une décision" : "En cours · il y a 12 s", Foreground = Brushes.White, Opacity = 0.8, FontSize = 13, Margin = new Thickness(0, 6, 0, 0) });
            text.Children.Add(new TextBlock { Text = "Dernière activité : Edit src/App.cs", Foreground = Brushes.White, Opacity = 0.6, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
            var panel = new Border
            {
                Width = 340, Height = 140, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1.5),
                BorderBrush = new SolidColorBrush(tint), Background = new SolidColorBrush(Color.FromArgb(0xD9, 0x06, 0x16, 0x24)),
                Child = text, Opacity = 0, RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup { Children = [new ScaleTransform(0.1, 0.1), new TranslateTransform()] },
            };
            var index = i;
            panel.MouseLeftButtonDown += (_, e) => { Hit?.Invoke(index); e.Handled = true; };
            Canvas.SetLeft(panel, pos.X); Canvas.SetTop(panel, pos.Y);
            _canvas.Children.Add(panel); _panels.Add(panel);
        }
    }

    /// <summary>Panels fly out of the orb (scale, translation, opacity) and the desktop darkens. 30 fps cap.</summary>
    public Task AnimateWpf(bool open)
    {
        var ms = open ? OpenMs : CloseMs;
        var done = new TaskCompletionSource();
        var duration = new Duration(TimeSpan.FromMilliseconds(ms));
        IEasingFunction ease = open ? new CubicEase { EasingMode = EasingMode.EaseOut } : new CubicEase { EasingMode = EasingMode.EaseIn };
        DoubleAnimation Anim(double to) { var a = new DoubleAnimation(to, duration) { EasingFunction = ease }; Timeline.SetDesiredFrameRate(a, 30); return a; }
        var last = Anim(open ? 1 : 0);
        last.Completed += (_, _) => done.TrySetResult();
        _dim.BeginAnimation(OpacityProperty, last);
        for (var i = 0; i < _panels.Count; i++)
        {
            var p = _panels[i];
            var pos = PanelPosition(i);
            var group = (TransformGroup)p.RenderTransform;
            var scale = (ScaleTransform)group.Children[0];
            var move = (TranslateTransform)group.Children[1];
            if (open) { move.X = _orbCentre.X - (pos.X + 170); move.Y = _orbCentre.Y - (pos.Y + 70); }
            move.BeginAnimation(TranslateTransform.XProperty, Anim(open ? 0 : _orbCentre.X - (pos.X + 170)));
            move.BeginAnimation(TranslateTransform.YProperty, Anim(open ? 0 : _orbCentre.Y - (pos.Y + 70)));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(open ? 1 : 0.1));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(open ? 1 : 0.1));
            p.BeginAnimation(OpacityProperty, Anim(open ? 1 : 0));
            _links[i].BeginAnimation(OpacityProperty, Anim(open ? 0.8 : 0));
        }
        return done.Task;
    }

    /// <summary>Sets the final state without animation and drops the animation clocks (nothing keeps ticking).</summary>
    public void FreezeWpf(bool open)
    {
        _dim.BeginAnimation(OpacityProperty, null); _dim.Opacity = open ? 1 : 0;
        for (var i = 0; i < _panels.Count; i++)
        {
            var group = (TransformGroup)_panels[i].RenderTransform;
            var scale = (ScaleTransform)group.Children[0]; var move = (TranslateTransform)group.Children[1];
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            move.BeginAnimation(TranslateTransform.XProperty, null); move.BeginAnimation(TranslateTransform.YProperty, null);
            _panels[i].BeginAnimation(OpacityProperty, null); _links[i].BeginAnimation(OpacityProperty, null);
            scale.ScaleX = scale.ScaleY = open ? 1 : 0.1; move.X = move.Y = 0; _panels[i].Opacity = open ? 1 : 0; _links[i].Opacity = open ? 0.8 : 0;
        }
    }

    // ---------- WebView2 rendering ----------

    void BuildWeb()
    {
        _web = new WebView2CompositionControl { DefaultBackgroundColor = System.Drawing.Color.Transparent };
        Content = _web;
        _webReady = new TaskCompletionSource<bool>();
        Loaded += async (_, _) =>
        {
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jarvis-e11-webview2"));
            await _web.EnsureCoreWebView2Async(env);
            _web.CoreWebView2.SetVirtualHostNameToFolderMapping("fan.local", System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot"), Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
            _web.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                var m = System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson).RootElement;
                switch (m.GetProperty("type").GetString())
                {
                    case "ready": _webReady.TrySetResult(true); break;
                    case "frame0": WebFirstFrame?.Invoke(); break;
                    case "done": _webDone?.TrySetResult((m.GetProperty("frames").GetInt32(), m.GetProperty("ms").GetDouble())); break;
                    case "hit": Hit?.Invoke(m.GetProperty("index").GetInt32()); break;
                }
            };
            _web.CoreWebView2.Navigate($"https://fan.local/fan.html?ox={_orbCentre.X:F0}&oy={_orbCentre.Y:F0}");
        };
    }

    public Task WebReady => _webReady!.Task;

    public async Task<(int Frames, double Ms)> AnimateWeb(bool open)
    {
        _webDone = new TaskCompletionSource<(int, double)>();
        await _web!.CoreWebView2.ExecuteScriptAsync(open ? $"fan.open({OpenMs})" : $"fan.close({CloseMs})");
        return await _webDone.Task;
    }

    Point WebPanelCentre(int i)
    {
        var pos = PanelPosition(i);
        return new Point(pos.X + 170, pos.Y + 70);
    }
}
