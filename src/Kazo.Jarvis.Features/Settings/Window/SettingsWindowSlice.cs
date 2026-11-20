using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Settings.Window;

/// <summary>Registration of the settings window slice (US1.2).</summary>
public static class SettingsWindowSlice
{
    /// <summary>Adds the handler of <see cref="SettingsWindowRequested"/> and the state of the window.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddSettingsWindowSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<SettingsWindowState>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<SettingsWindowRequested>, SettingsWindowOpener>());
        return services;
    }
}
