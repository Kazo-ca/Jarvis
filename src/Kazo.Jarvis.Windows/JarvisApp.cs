using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Kazo.Jarvis.Adapters.Logging;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Windows.Desktop;
using Kazo.Jarvis.Windows.Dialogs;
using Kazo.Jarvis.Windows.Orb;
using Kazo.Jarvis.Windows.Tray;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace Kazo.Jarvis.Windows;

/// <summary>
/// The WPF application of the Windows host. Start-up order: WebView2 check, tray icon, then the orb as soon
/// as the session desktop can show it (a locked session waits for the unlock instead of dying), then the
/// background services of the generic host, without blocking the UI thread (ADR 0013, section 4). Quit
/// stops them within 10 seconds, then ends the application.
/// </summary>
internal sealed partial class JarvisApp : Application
{
    /// <summary>Direct3D 9 not available (locked session, secure desktop): <c>D3DERR_NOTAVAILABLE</c>.</summary>
    private const int D3dNotAvailable = unchecked((int)0x8876086A);

    /// <summary>The Direct3D 9 device was lost (sleep, lock, driver reset): <c>D3DERR_DEVICELOST</c>.</summary>
    private const int D3dDeviceLost = unchecked((int)0x88760868);

    private static readonly TimeSpan DesktopPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

    private readonly IHost host;
    private readonly SingleInstance instance;
    private readonly ILogger<JarvisApp> logger;
    private readonly OrbWindowService orb;
    private readonly WindowDialogService dialogs;
    private readonly TrayIcon tray;
    private readonly FileLoggerProvider log;
    private DispatcherTimer? desktopWait;
    private Task? backgroundStart;
    private bool quitting;

    /// <summary>Creates the application over the built host.</summary>
    /// <param name="host">The generic host, built but not started.</param>
    /// <param name="instance">The single instance claim, to answer a second launch.</param>
    public JarvisApp(IHost host, SingleInstance instance)
    {
        this.host = host;
        this.instance = instance;
        logger = host.Services.GetRequiredService<ILogger<JarvisApp>>();
        orb = host.Services.GetRequiredService<OrbWindowService>();
        dialogs = host.Services.GetRequiredService<WindowDialogService>();
        tray = host.Services.GetRequiredService<TrayIcon>();
        log = host.Services.GetRequiredService<FileLoggerProvider>();

        // From here on the host texts follow the assistant language, then its changes (HostLanguage).
        HostTexts.Culture = host.Services.GetRequiredService<IAssistantLanguageReader>().Current.Culture;

        // The orb may be hidden or closed (locked session): only Quit ends the application.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        LogStarting(Environment.ProcessPath ?? "?", Environment.ProcessId, log.CurrentFile);

        if (!IsWebView2Installed())
        {
            Shutdown(1);
            return;
        }

        tray.QuitRequested += (_, _) => Quit(0);
        tray.Show();
        orb.Rendered += (_, _) => StartBackgroundServices();
        instance.OnShowRequested(() => Dispatcher.BeginInvoke(() =>
        {
            LogSecondLaunch();
            orb.Show();
        }));

        OpenOrbWhenDesktopIsReady();
    }

    /// <summary>Opens the orb now when the desktop can show it; otherwise waits for the unlock.</summary>
    private void OpenOrbWhenDesktopIsReady()
    {
        if (quitting || orb.IsOpen || desktopWait is not null)
        {
            return;
        }

        if (SessionDesktop.IsReady(out var reason))
        {
            orb.Open();
            return;
        }

        LogWaitingForDesktop(reason ?? "unknown");
        tray.SetStatus(HostTexts.WaitingForUnlock);
        var readySince = (DateTime?)null;
        desktopWait = new DispatcherTimer(DesktopPollInterval, DispatcherPriority.Background, (_, _) =>
        {
            if (!SessionDesktop.IsReady(out _))
            {
                readySince = null;
                return;
            }

            // Direct3D may lag the unlock by a moment: wait until the desktop has been ready for a while.
            readySince ??= DateTime.UtcNow;
            if (DateTime.UtcNow - readySince < SettleDelay)
            {
                return;
            }

            desktopWait?.Stop();
            desktopWait = null;
            tray.SetStatus(null);
            LogDesktopReady();
            orb.Open();
        }, Dispatcher);
        desktopWait.Start();
    }

    /// <summary>Starts the background services once, after the orb is drawn, off the UI thread.</summary>
    private void StartBackgroundServices()
    {
        if (backgroundStart is not null || quitting)
        {
            return;
        }

        backgroundStart = Task.Run(async () =>
        {
            try
            {
                await host.StartAsync().ConfigureAwait(false);
                LogBackgroundStarted();
            }
            catch (Exception exception)
            {
                LogBackgroundStartFailed(exception);
            }
        });
    }

    /// <summary>Quit: hides everything, stops the background services within 10 seconds, then ends.</summary>
    private async void Quit(int exitCode)
    {
        if (quitting)
        {
            return;
        }

        quitting = true;
        LogQuitting(exitCode);
        desktopWait?.Stop();
        tray.Hide();
        dialogs.CloseAll();
        orb.Close();

        try
        {
            using var timeout = new CancellationTokenSource(StopTimeout);
            await Task.Run(async () =>
            {
                if (backgroundStart is not null)
                {
                    await backgroundStart.ConfigureAwait(false);
                    await host.StopAsync(timeout.Token).ConfigureAwait(false);
                }
            }).WaitAsync(StopTimeout).ConfigureAwait(true);
            LogStopped();
        }
        catch (Exception exception)
        {
            LogStopFailed(exception);
        }

        Shutdown(exitCode);
    }

    private bool IsWebView2Installed()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            LogWebView2(version);
            return true;
        }
        catch (WebView2RuntimeNotFoundException exception)
        {
            LogWebView2Missing(exception);
            var answer = MessageBox.Show(HostTexts.WebView2Missing, HostTexts.ApplicationName, MessageBoxButton.YesNo, MessageBoxImage.Error);
            if (answer == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(HostTexts.WebView2Download) { UseShellExecute = true });
            }

            return false;
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (IsDesktopUnavailable(e.Exception) && !quitting)
        {
            // The orb's web view lost Direct3D (the session locked as it was drawing, or a resume from
            // sleep). Drop the window and open a new one once the desktop is back, instead of dying.
            e.Handled = true;
            LogDesktopLost(e.Exception);
            orb.Close();
            Dispatcher.BeginInvoke(OpenOrbWhenDesktopIsReady, DispatcherPriority.Background);
            return;
        }

        e.Handled = true;
        LogCrashed(e.Exception);
        if (!quitting)
        {
            MessageBox.Show(HostTexts.Crashed(log.CurrentFile), HostTexts.ApplicationName, MessageBoxButton.OK, MessageBoxImage.Error);
            Quit(1);
        }
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        LogCrashed(e.ExceptionObject as Exception);

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogUnobserved(e.Exception);
        e.SetObserved();
    }

    private static bool IsDesktopUnavailable(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.HResult is D3dNotAvailable or D3dDeviceLost
                || (current is COMException com && com.ErrorCode is D3dNotAvailable or D3dDeviceLost))
            {
                return true;
            }

            if (current is AggregateException aggregate && aggregate.InnerExceptions.Any(IsDesktopUnavailable))
            {
                return true;
            }
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Kazo.Jarvis starting: {Executable}, process {ProcessId}, log {LogFile}.")]
    private partial void LogStarting(string executable, int processId, string logFile);

    [LoggerMessage(Level = LogLevel.Information, Message = "WebView2 runtime {Version} found.")]
    private partial void LogWebView2(string version);

    [LoggerMessage(Level = LogLevel.Critical, Message = "The WebView2 runtime is missing; the user was told how to install it.")]
    private partial void LogWebView2Missing(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The session desktop cannot show the orb yet ({Reason}). Waiting for the unlock; the tray icon says so.")]
    private partial void LogWaitingForDesktop(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "The session desktop is ready: opening the orb.")]
    private partial void LogDesktopReady();

    [LoggerMessage(Level = LogLevel.Warning, Message = "The orb window lost Direct3D (locked session or resume from sleep); it opens again once the desktop is ready.")]
    private partial void LogDesktopLost(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Second launch: showing the orb of this instance.")]
    private partial void LogSecondLaunch();

    [LoggerMessage(Level = LogLevel.Information, Message = "Background services started.")]
    private partial void LogBackgroundStarted();

    [LoggerMessage(Level = LogLevel.Error, Message = "The background services could not start.")]
    private partial void LogBackgroundStartFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Quitting (exit code {ExitCode}).")]
    private partial void LogQuitting(int exitCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Background services stopped.")]
    private partial void LogStopped();

    [LoggerMessage(Level = LogLevel.Error, Message = "The background services did not stop cleanly within 10 seconds.")]
    private partial void LogStopFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception: Kazo.Jarvis stops.")]
    private partial void LogCrashed(Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "A background task failed without being observed.")]
    private partial void LogUnobserved(Exception exception);
}
