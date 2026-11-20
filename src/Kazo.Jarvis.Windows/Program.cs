using System.Windows.Threading;
using Kazo.Jarvis.Adapters;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Adapters.Logging;
using Kazo.Jarvis.Features;
using Kazo.Jarvis.Windows.Desktop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Windows;

/// <summary>
/// Entry point of the Windows host: claims the single instance, builds the generic host (ADR 0013,
/// section 4), then runs the WPF application.
/// </summary>
public static class Program
{
    /// <summary>Starts Kazo.Jarvis, or shows the orb of the instance already running.</summary>
    /// <param name="args">Command line arguments, given to the host.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        using var instance = SingleInstance.Claim();
        if (!instance.IsFirst)
        {
            // A second launch shows the orb of the running instance, then ends (ADR 0003, section 2).
            instance.SignalFirst();
            return 0;
        }

        // Transparent web views from their creation: no white flash behind the orb.
        Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "00000000");

        // Every file goes under %USERPROFILE%\.kazo\jarvis (ADR 0004, section 1).
        var paths = JarvisPaths.ForCurrentUser();
        paths.EnsureCreated();

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ApplicationName = "Kazo.Jarvis",
            ContentRootPath = AppContext.BaseDirectory,
            // No appsettings.json, environment configuration or console log: config.json is the only setting file (ADR 0004).
            DisableDefaults = true,
        });

        var fileLog = new FileLoggerProvider(paths.Logs, TimeProvider.System);
        builder.Logging.AddProvider(fileLog);
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Services.AddSingleton(fileLog);

        // The WPF application decides when the host stops, within 10 seconds (ADR 0013, section 4).
        builder.Services.AddSingleton<IHostLifetime, DesktopLifetime>();
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(10));

        // Main runs on the UI thread: every window and native hook goes through its dispatcher.
        builder.Services.AddSingleton(Dispatcher.CurrentDispatcher);
        builder.Services.AddWpfBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        // Contracts first (real adapters, then the host's native services), then the slices (ADR 0002, section 6).
        builder.Services.AddJarvisAdapters(paths);
        builder.Services.AddJarvisWindowsHost();
        builder.Services.AddJarvisFeatures();

        using var host = builder.Build();
        var application = new JarvisApp(host, instance);
        return application.Run();
    }
}
