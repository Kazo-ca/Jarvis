using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Assistant.Orb;

/// <summary>Registration of the orb slice (US1.3, US1.6; ADR 0005 and 0011).</summary>
public static class OrbSlice
{
    /// <summary>Adds what the orb components need besides the kernel: the click activation.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddOrbSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<OrbActivation>();
        return services;
    }
}
