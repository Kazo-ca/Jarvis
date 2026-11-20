using Kazo.Jarvis.Kernel.Messages.Updates;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Maintenance.Updates;

/// <summary>Registration of the updates slice (US1.18).</summary>
public static class UpdatesSlice
{
    /// <summary>
    /// Adds the update service, the automatic check (a background service) and the window opener. The host
    /// provides <see cref="Kernel.Updates.IReleaseSource"/>, <see cref="Kernel.Updates.IUpdateLauncher"/> and
    /// <see cref="Kernel.Desktop.IDialogService"/>.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddUpdatesSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<UpdateService>();
        services.TryAddSingleton<AutomaticUpdateCheck>();
        services.AddHostedService(provider => provider.GetRequiredService<AutomaticUpdateCheck>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<UpdatesWindowRequested>, UpdatesWindowOpener>());
        return services;
    }
}
