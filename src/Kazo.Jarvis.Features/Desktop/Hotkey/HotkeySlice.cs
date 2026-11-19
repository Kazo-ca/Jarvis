using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Desktop.Hotkey;

/// <summary>Registration of the hotkey slice.</summary>
public static class HotkeySlice
{
    /// <summary>Adds the hotkey binder, a background service. The host provides <see cref="Kernel.Desktop.IGlobalHotkey"/>.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddHotkeySlice(this IServiceCollection services)
    {
        services.TryAddSingleton<HotkeyBinder>();
        services.AddHostedService(provider => provider.GetRequiredService<HotkeyBinder>());
        return services;
    }
}
