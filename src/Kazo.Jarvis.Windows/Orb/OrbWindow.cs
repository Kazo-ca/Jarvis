using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Kazo.Jarvis.Windows.Desktop;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace Kazo.Jarvis.Windows.Orb;

/// <summary>
/// The orb window (ADR 0003, section 2; US1.1): transparent, borderless, always on top, out of the taskbar
/// and of Alt+Tab, never takes the focus. Clicks on the transparent pixels around the orb reach the
/// application below (layered window). Pressing on the orb and dragging moves it; pressing without moving
/// is a click.
/// </summary>
internal sealed class OrbWindow : Window
{
    /// <summary>Width and height of the window, in device-independent pixels.</summary>
    public const double Size = 160;

    /// <summary>Below this distance, in device-independent pixels, a press is a click, not a move.</summary>
    private const double ClickTolerance = 4;

    /// <summary>Creates the window on the shared service provider.</summary>
    /// <param name="services">Provider shared by every window of the host (ADR 0003).</param>
    /// <param name="webViewDataFolder">The WebView2 profile folder, outside the installation folder.</param>
    public OrbWindow(IServiceProvider services, string webViewDataFolder)
    {
        Title = HostTexts.ApplicationName;
        Width = Size;
        Height = Size;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var webView = new BlazorWebView
        {
            HostPage = @"wwwroot\orb.html",
            Services = services,
        };
        webView.BlazorWebViewInitializing += (_, e) => e.UserDataFolder = webViewDataFolder;
        webView.BlazorWebViewInitialized += (_, e) => e.WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
        webView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Shell),
        });
        Content = webView;

        // WebView2CompositionControl receives its input through WPF, so the window sees the press first.
        PreviewMouseLeftButtonDown += OnPress;
        PreviewMouseRightButtonDown += OnRightPress;
        PreviewMouseRightButtonUp += OnRightRelease;
        SourceInitialized += (_, _) => NativeMethods.AddExtendedStyle(
            new WindowInteropHelper(this).Handle, NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow);
    }

    /// <summary>The top-left corner in physical screen pixels.</summary>
    public (int X, int Y) ScreenPosition => NativeMethods.WindowPosition(new WindowInteropHelper(this).Handle);

    /// <summary>The window's rectangle in physical screen pixels.</summary>
    public (int X, int Y, int Width, int Height) ScreenBounds => NativeMethods.WindowBounds(new WindowInteropHelper(this).Handle);

    /// <summary>Puts the window back above the other "always on top" windows of the host, without activating it.</summary>
    public void BringToTop() => NativeMethods.BringToTop(new WindowInteropHelper(this).Handle);

    /// <summary>
    /// Places the window at physical screen pixels as soon as its handle exists, before it is shown. Windows
    /// then sends the scale change of the target screen, and WPF resizes the window for it.
    /// </summary>
    /// <param name="x">Left edge in physical pixels.</param>
    /// <param name="y">Top edge in physical pixels.</param>
    public void PlaceAtScreenPosition(int x, int y) =>
        SourceInitialized += (_, _) => NativeMethods.MoveWindow(new WindowInteropHelper(this).Handle, x, y);

    /// <summary>Raised on a press released without moving the window.</summary>
    public event EventHandler? Clicked;

    /// <summary>Raised on a right click (ADR 0027, section 2). The WebView never sees it, so it shows no menu.</summary>
    public event EventHandler? RightClicked;

    /// <summary>Raised after the user moved the window.</summary>
    public event EventHandler? Moved;

    // Kept from the WebView, which would otherwise open its own menu on the release.
    private void OnRightPress(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void OnRightRelease(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        RightClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnPress(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var left = Left;
        var top = Top;
        try
        {
            // Returns when the button is released.
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The button was already released.
            return;
        }

        if (Math.Abs(Left - left) < ClickTolerance && Math.Abs(Top - top) < ClickTolerance)
        {
            Left = left;
            Top = top;
            Clicked?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Moved?.Invoke(this, EventArgs.Empty);
        }
    }
}
