using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Messages.Updates;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Updates;
using Kazo.Jarvis.Windows.Desktop;
using Kazo.Jarvis.Windows.Dialogs;
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
    /// <summary>Orb window, dialogs, tray icon, global hotkey, start with Windows and updater launcher.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJarvisWindowsHost(this IServiceCollection services)
    {
        services.TryAddSingleton<OrbWindowService>();
        services.TryAddSingleton<IOrbWindow>(provider => provider.GetRequiredService<OrbWindowService>());
        services.TryAddSingleton<WindowDialogService>();
        services.TryAddSingleton<IDialogService>(provider => provider.GetRequiredService<WindowDialogService>());
        services.TryAddSingleton<IGlobalHotkey, Win32GlobalHotkey>();
        services.TryAddSingleton<ILaunchAtLogin, RegistryLaunchAtLogin>();
        services.TryAddSingleton<TrayIcon>();

        // Updates (US1.18): starts the updater, tells about a new version, cleans what the last update left.
        services.TryAddSingleton<QuitSignal>();
        services.TryAddSingleton<IUpdateLauncher, UpdaterLauncher>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<UpdateFound>, UpdateFoundNotice>());
        services.AddHostedService<UpdateLeftovers>();
        return services;
    }
}
