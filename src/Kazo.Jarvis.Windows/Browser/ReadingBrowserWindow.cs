using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Kazo.Jarvis.Windows.Desktop;

namespace Kazo.Jarvis.Windows.Browser;

/// <summary>
/// The window of the browser of Jarvis (ADR 0022, section 1). While Jarvis uses it, it is never shown: no taskbar
/// button, no activation, so it never takes the focus (spike E15). When the user opens it, it becomes an ordinary
/// window with an address bar, « Précédent » and « Recharger » ; closing it hides it again and keeps the browser.
/// The WebView2 controller is attached to its handle by <see cref="WebViewReadingBrowser"/> and drawn under the bar.
/// </summary>
internal sealed class ReadingBrowserWindow : Window
{
    private const int HiddenStyles = NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow;

    private readonly TextBox address;
    private readonly Border view;
    private bool closingForGood;

    /// <summary>Creates the window, hidden. Its handle exists at once; it is not shown.</summary>
    public ReadingBrowserWindow()
    {
        Title = HostTexts.BrowserTitle;
        Width = 1100;
        Height = 800;
        MinWidth = 480;
        MinHeight = 360;
        ShowInTaskbar = false;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x12, 0x1A));

        var back = Button(HostTexts.BrowserBack);
        back.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
        var reload = Button(HostTexts.BrowserReload);
        reload.Click += (_, _) => ReloadRequested?.Invoke(this, EventArgs.Empty);
        address = new TextBox
        {
            Margin = new Thickness(4),
            Padding = new Thickness(6, 3, 6, 3),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = HostTexts.BrowserAddress,
        };
        System.Windows.Automation.AutomationProperties.SetName(address, HostTexts.BrowserAddress);
        address.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(address.Text))
            {
                NavigateRequested?.Invoke(this, address.Text.Trim());
            }
        };

        var bar = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(back, Dock.Left);
        DockPanel.SetDock(reload, Dock.Left);
        bar.Children.Add(back);
        bar.Children.Add(reload);
        bar.Children.Add(address);

        // What signing in here means, always in view (ADR 0022, section 2).
        var warning = new TextBlock
        {
            Text = HostTexts.BrowserWarning,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 8, 6),
            Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xB9, 0x42)),
            FontSize = 12,
        };

        view = new Border();
        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(warning, Dock.Top);
        layout.Children.Add(bar);
        layout.Children.Add(warning);
        layout.Children.Add(view);
        Content = layout;

        Handle = new WindowInteropHelper(this).EnsureHandle();
        NativeMethods.AddExtendedStyle(Handle, HiddenStyles);
        view.SizeChanged += (_, _) => ViewChanged?.Invoke(this, EventArgs.Empty);
        DpiChanged += (_, _) => ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The handle the WebView2 controller is attached to.</summary>
    public IntPtr Handle { get; }

    /// <summary>Whether the user has the window open.</summary>
    public bool IsOpenForUser { get; private set; }

    /// <summary>Raised when the user typed an address and pressed Enter.</summary>
    public event EventHandler<string>? NavigateRequested;

    /// <summary>Raised when the user pressed « Précédent ».</summary>
    public event EventHandler? BackRequested;

    /// <summary>Raised when the user pressed « Recharger ».</summary>
    public event EventHandler? ReloadRequested;

    /// <summary>Raised when the place of the page in the window changed: the controller must follow.</summary>
    public event EventHandler? ViewChanged;

    /// <summary>
    /// Where the page is drawn, in physical pixels of the client area: under the bar once the window was laid out,
    /// the whole client area before that (the window was never shown).
    /// </summary>
    public System.Drawing.Rectangle ViewBounds
    {
        get
        {
            if (view.ActualWidth < 1 || view.ActualHeight < 1 || PresentationSource.FromVisual(this) is null)
            {
                var (width, height) = NativeMethods.ClientSize(Handle);
                return new System.Drawing.Rectangle(0, 0, Math.Max(1, width), Math.Max(1, height));
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            var origin = view.TransformToAncestor(this).Transform(new Point(0, 0));
            return new System.Drawing.Rectangle(
                (int)Math.Round(origin.X * dpi.DpiScaleX),
                (int)Math.Round(origin.Y * dpi.DpiScaleY),
                Math.Max(1, (int)Math.Round(view.ActualWidth * dpi.DpiScaleX)),
                Math.Max(1, (int)Math.Round(view.ActualHeight * dpi.DpiScaleY)));
        }
    }

    /// <summary>Shows the address of the page in the bar, unless the user is typing there.</summary>
    /// <param name="value">The address of the page.</param>
    public void ShowAddress(string value)
    {
        if (!address.IsKeyboardFocused)
        {
            address.Text = value == "about:blank" ? string.Empty : value;
        }
    }

    /// <summary>Opens the window for the user: an ordinary window, in the taskbar, that takes the focus.</summary>
    public void OpenForUser()
    {
        IsOpenForUser = true;
        NativeMethods.RemoveExtendedStyle(Handle, HiddenStyles);
        ShowInTaskbar = true;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
    }

    /// <summary>Closes the window for good, with the browser.</summary>
    public void CloseForGood()
    {
        closingForGood = true;
        Close();
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnClosing(e);
        if (closingForGood)
        {
            return;
        }

        // Closing the window does not close the browser (ADR 0022, section 1): it goes back to never taking the
        // focus, and stops by itself after a time without use.
        e.Cancel = true;
        Hide();
        ShowInTaskbar = false;
        NativeMethods.AddExtendedStyle(Handle, HiddenStyles);
        IsOpenForUser = false;
    }

    private static Button Button(string text) => new()
    {
        Content = text,
        Margin = new Thickness(4, 4, 0, 4),
        Padding = new Thickness(10, 3, 10, 3),
    };
}
