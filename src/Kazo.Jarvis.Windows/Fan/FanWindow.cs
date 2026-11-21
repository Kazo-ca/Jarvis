using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Kazo.Jarvis.Windows.Desktop;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace Kazo.Jarvis.Windows.Fan;

/// <summary>
/// The window of the task fan (ADR 0010, section 5): transparent, borderless, always on top, out of the taskbar,
/// over the work area of the screen the orb is on. It is created once and kept hidden (spike E11: a web view
/// created at each opening takes more than two seconds to show its first image). The fan slice draws the panels
/// in it; where nothing is drawn, the window lets the clicks through.
/// </summary>
internal sealed class FanWindow : Window
{
    /// <summary>Creates the window on the shared service provider.</summary>
    /// <param name="services">Provider shared by every window of the host (ADR 0003).</param>
    /// <param name="webViewDataFolder">The WebView2 profile folder, the same for every window.</param>
    public FanWindow(IServiceProvider services, string webViewDataFolder)
    {
        Title = HostTexts.ApplicationName;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;

        // Not activated when it is prepared at start-up; the service activates it when the fan opens, for Escape.
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var webView = new BlazorWebView
        {
            HostPage = @"wwwroot\fan.html",
            Services = services,
        };
        webView.BlazorWebViewInitializing += (_, e) => e.UserDataFolder = webViewDataFolder;
        webView.BlazorWebViewInitialized += (_, e) =>
        {
            e.WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            Ready?.Invoke(this, EventArgs.Empty);
        };
        webView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(FanShell),
        });
        Content = webView;

        // The web view receives its input through WPF, so the window sees the key first, whatever has the focus
        // in the page.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                EscapePressed?.Invoke(this, EventArgs.Empty);
            }
        };
        SourceInitialized += (_, _) => NativeMethods.AddExtendedStyle(new WindowInteropHelper(this).Handle, NativeMethods.WsExToolWindow);
    }

    /// <summary>Raised once the web view of the window is created.</summary>
    public event EventHandler? Ready;

    /// <summary>Raised when Escape is pressed while the window has the focus.</summary>
    public event EventHandler? EscapePressed;

    /// <summary>Places the window over a rectangle of physical screen pixels, without activating it.</summary>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    public void Cover(int x, int y, int width, int height) =>
        NativeMethods.CoverTopmost(new WindowInteropHelper(this).EnsureHandle(), x, y, width, height);
}
