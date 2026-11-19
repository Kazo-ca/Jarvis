using System.Windows;
using Kazo.Jarvis.Adapters;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Windows;

/// <summary>WPF entry point: builds the service container and shows the main window.</summary>
public static class Program
{
    /// <summary>Starts the WPF application.</summary>
    /// <returns>The process exit code.</returns>
    [STAThread]
    public static int Main()
    {
        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        // Every file goes under %USERPROFILE%\.kazo\jarvis (ADR 0004, section 1).
        var paths = JarvisPaths.ForCurrentUser();
        paths.EnsureCreated();

        // Contracts first (real adapters), then the slices (ADR 0002, section 6).
        services.AddJarvisAdapters(paths);
        services.AddJarvisFeatures();

        using var provider = services.BuildServiceProvider();

        // The window is built in code, like in Kazo Bar: no XAML to compile.
        var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        return application.Run(new MainWindow(provider));
    }
}
