using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Features.Tools.Catalog;

/// <summary>Registration of the « Outils » page (ADR 0017, section 6).</summary>
public static class ToolCatalogSlice
{
    /// <summary>Key of the tools page in the settings window.</summary>
    public const string PageKey = "Tools.Catalog";

    /// <summary>Adds the page that lists every tool of the registry with its switch.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddToolCatalogSlice(this IServiceCollection services)
    {
        // Between the confirmation page (45) and the pages of the tool families (47, 48).
        services.AddSingleton(new SettingsPage(PageKey, typeof(ToolsPage), typeof(ToolsPage), "Title", Order: 46));
        return services;
    }
}
