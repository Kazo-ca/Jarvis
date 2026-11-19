using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Adapters;

/// <summary>Registers the real implementations of the kernel contracts.</summary>
public static class AdaptersServiceCollectionExtensions
{
    /// <summary>
    /// Adds every adapter to <paramref name="services"/>. Only the Windows host calls it, before
    /// <c>AddJarvisFeatures</c>, so that slices find their contracts (ADR 0002, section 6).
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJarvisAdapters(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Adapters arrive with the work items that need them (stage1/3-settings-storage-vault onwards).
        return services;
    }
}
