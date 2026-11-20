using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kazo.Jarvis.Features.Assistant.Faults;

/// <summary>
/// Registration of the faults slice (US1.17, work item stage1/15-faults). Each slice raises and clears its own
/// causes; this one names them, says what to do, links to the setting that fixes them, and raises the cause no
/// slice owns: a settings file that cannot be used.
/// </summary>
public static class FaultsSlice
{
    /// <summary>Adds the faults window, its opener and the settings file check.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddFaultsSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<FaultWindowOpener>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<FaultWindowRequested>, FaultWindowOpener>(Opener));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<ActivationRefused>, FaultWindowOpener>(Opener));

        services.TryAddSingleton<ConfigurationFaultReporter>();
        services.AddHostedService(provider => provider.GetRequiredService<ConfigurationFaultReporter>());
        return services;
    }

    private static FaultWindowOpener Opener(IServiceProvider provider) => provider.GetRequiredService<FaultWindowOpener>();
}
