using System.Windows;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace Kazo.Jarvis.Windows;

/// <summary>
/// Empty window hosting <see cref="BlazorWebView"/>. It proves the host starts and renders the
/// root component. Work item stage1/4-hosts replaces it with the transparent orb window (ADR 0003).
/// </summary>
internal sealed class MainWindow : Window
{
    /// <summary>Creates the window on the shared service provider.</summary>
    /// <param name="services">Provider shared by every window of the host (ADR 0003).</param>
    public MainWindow(IServiceProvider services)
    {
        Title = "Kazo.Jarvis";
        Width = 480;
        Height = 320;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var webView = new BlazorWebView
        {
            HostPage = @"wwwroot\index.html",
            Services = services,
        };
        webView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Shell),
        });

        Content = webView;
    }
}
