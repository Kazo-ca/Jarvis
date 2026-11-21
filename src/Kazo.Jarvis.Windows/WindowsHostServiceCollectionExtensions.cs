using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Localization;
using Kazo.Jarvis.Kernel.Messages.Updates;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Updates;
using Kazo.Jarvis.Kernel.Web;
using Kazo.Jarvis.Windows.Audio;
using Kazo.Jarvis.Windows.Browser;
using Kazo.Jarvis.Windows.Desktop;
using Kazo.Jarvis.Windows.Dialogs;
using Kazo.Jarvis.Windows.Fan;
using Kazo.Jarvis.Windows.Orb;
using Kazo.Jarvis.Windows.Startup;
using Kazo.Jarvis.Windows.Tray;
using Kazo.Jarvis.Windows.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Windows;

/// <summary>Registers the native services of the Windows host (ADR 0003, section 5).</summary>
internal static class WindowsHostServiceCollectionExtensions
{
    /// <summary>Orb window, fan window, dialogs, tray icon, global hotkey, audio devices, start with Windows, Windows display language, the browser of Jarvis and updater launcher.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJarvisWindowsHost(this IServiceCollection services)
    {
        services.TryAddSingleton<OrbWindowService>();
        services.TryAddSingleton<IOrbWindow>(provider => provider.GetRequiredService<OrbWindowService>());
        // The task fan (ADR 0010): its window, and the double-click time that delays a single click on the orb.
        services.TryAddSingleton<FanWindowService>();
        services.TryAddSingleton<ITaskFanSurface>(provider => provider.GetRequiredService<FanWindowService>());
        services.TryAddSingleton<IPointerTimings, SystemPointerTimings>();
        services.TryAddSingleton<WindowDialogService>();
        services.TryAddSingleton<IDialogService>(provider => provider.GetRequiredService<WindowDialogService>());
        services.TryAddSingleton<IGlobalHotkey, Win32GlobalHotkey>();
        services.TryAddSingleton<ILaunchAtLogin, RegistryLaunchAtLogin>();
        services.TryAddSingleton<TrayIcon>();

        // Audio devices (US1.13): WASAPI capture and playback, sharing one endpoint enumerator.
        services.TryAddSingleton<WasapiDevices>();
        services.TryAddSingleton<IAudioCapture, WasapiAudioCapture>();
        services.TryAddSingleton<IAudioPlayback, WasapiAudioPlayback>();
        services.TryAddSingleton<ISystemLanguage, WindowsSystemLanguage>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<AssistantLanguageChanged>, HostLanguage>());

        // The browser of Jarvis (ADR 0022): WebView2 in a window of its own, created at the first search. It asks the
        // address policy of the web tools slice at every request.
        services.TryAddSingleton<WebViewReadingBrowser>();
        services.TryAddSingleton<IReadingBrowser>(provider => provider.GetRequiredService<WebViewReadingBrowser>());

        // Updates (US1.18): starts the updater, tells about a new version, cleans what the last update left.
        services.TryAddSingleton<QuitSignal>();
        services.TryAddSingleton<IUpdateLauncher, UpdaterLauncher>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<UpdateFound>, UpdateFoundNotice>());
        services.AddHostedService<UpdateLeftovers>();
        return services;
    }
}
