using Kazo.Jarvis.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Features;

/// <summary>Registers every slice (ADR 0002, section 6).</summary>
public static class FeaturesServiceCollectionExtensions
{
    /// <summary>
    /// Adds every slice to <paramref name="services"/>. Each slice exposes its own registration
    /// method; this one calls them all. Hosts call it after registering the contracts.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJarvisFeatures(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The slices talk through the kernel's bus and read its state machine (ADR 0002, section 5).
        services.AddJarvisKernel();

        // Slices arrive with their work items (stage1/5-shared-ui-language onwards).
        return services;
    }
}
