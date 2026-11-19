using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Windows.Desktop;
using Kazo.Jarvis.Windows.Dialogs;
using Kazo.Jarvis.Windows.Orb;
using Kazo.Jarvis.Windows.Startup;
using Kazo.Jarvis.Windows.Tray;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Windows;

/// <summary>Registers the native services of the Windows host (ADR 0003, section 5).</summary>
internal static class WindowsHostServiceCollectionExtensions
{
    /// <summary>Orb window, dialogs, tray icon, global hotkey and start with Windows.</summary>
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
        return services;
    }
}
