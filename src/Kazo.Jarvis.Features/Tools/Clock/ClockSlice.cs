using Kazo.Jarvis.Kernel.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Tools.Clock;

/// <summary>Registration of the tool <c>get_datetime</c> (ADR 0017, section 3).</summary>
public static class ClockSlice
{
    /// <summary>Adds the tool to the registry of the kernel.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddClockToolSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, ClockTool>());
        return services;
    }
}
