using System.Windows;
using Kazo.Jarvis.Kernel.Desktop;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace Kazo.Jarvis.Windows.Dialogs;

/// <summary>
/// A normal, resizable window showing one slice component (ADR 0003, section 2). It has its own web view
/// but the provider shared by every window, so a change made here is seen by the others at once.
/// </summary>
internal sealed class DialogWindow : Window
{
    /// <summary>Creates the window for <paramref name="request"/>.</summary>
    /// <param name="services">Provider shared by every window of the host.</param>
    /// <param name="request">The component to show.</param>
    /// <param name="webViewDataFolder">The WebView2 profile folder, the same for every window.</param>
    public DialogWindow(IServiceProvider services, DialogRequest request, string webViewDataFolder)
    {
        Title = request.Title;
        Width = 720;
        Height = 560;
        MinWidth = 360;
        MinHeight = 240;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (request.Maximized)
        {
            WindowState = WindowState.Maximized;
        }

        var webView = new BlazorWebView
        {
            HostPage = @"wwwroot\index.html",
            Services = services,
        };
        webView.BlazorWebViewInitializing += (_, e) => e.UserDataFolder = webViewDataFolder;
        webView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(DialogRoot),
            Parameters = new Dictionary<string, object?>
            {
                [nameof(DialogRoot.Component)] = request.Component,
                [nameof(DialogRoot.Parameters)] = request.Parameters?.ToDictionary(item => item.Key, item => item.Value!),
            },
        });
        Content = webView;
    }
}
