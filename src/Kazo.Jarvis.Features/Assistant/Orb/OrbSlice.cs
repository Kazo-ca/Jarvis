using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Assistant.Orb;

/// <summary>Registration of the orb slice (US1.3, US1.6; ADR 0005 and 0011).</summary>
public static class OrbSlice
{
    /// <summary>Key of the orb page in the settings window.</summary>
    public const string PageKey = "Assistant.Orb";

    /// <summary>
    /// Adds what the orb components need besides the kernel: the click activation, the "orb quality" setting
    /// and its settings page.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddOrbSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<OrbActivation>();
        services.TryAddSingleton<OrbQualityKeeper>();

        // After the language page (order 0).
        services.AddSingleton(new SettingsPage(PageKey, typeof(OrbSettingsPage), typeof(OrbSettingsPage), "Title", Order: 10));
        return services;
    }
}
