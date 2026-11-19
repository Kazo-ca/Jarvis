using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Desktop.LaunchAtLogin;

/// <summary>Registration of the start with Windows slice.</summary>
public static class LaunchAtLoginSlice
{
    /// <summary>Adds the binder, a background service. The host provides <see cref="Kernel.Desktop.ILaunchAtLogin"/>.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddLaunchAtLoginSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<LaunchAtLoginBinder>();
        services.AddHostedService(provider => provider.GetRequiredService<LaunchAtLoginBinder>());
        return services;
    }
}
