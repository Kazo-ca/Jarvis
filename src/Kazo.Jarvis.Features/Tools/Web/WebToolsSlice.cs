using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Kernel.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Tools.Web;

/// <summary>Registration of the web tools <c>web_search</c> and <c>fetch_url</c> (ADR 0017, section 3; ADR 0022).</summary>
public static class WebToolsSlice
{
    /// <summary>Key of the web page in the settings window.</summary>
    public const string PageKey = "Tools.Web";

    /// <summary>
    /// Adds the address policy, the two tools and their page. The browser of Jarvis (<see cref="IReadingBrowser"/>)
    /// and the search by API (<see cref="IWebSearchApi"/>) come from the host; a tool without them says so when called.
    /// Both tools are off until the user turns them on.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddWebToolsSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<WebAddressPolicy>();
        services.TryAddSingleton<IWebAddressPolicy>(provider => provider.GetRequiredService<WebAddressPolicy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, FetchUrlTool>(provider => new FetchUrlTool(
            provider.GetRequiredService<ISettingsStore>(),
            provider.GetRequiredService<IWebAddressPolicy>(),
            provider.GetService<IReadingBrowser>())));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, WebSearchTool>(provider => new WebSearchTool(
            provider.GetRequiredService<ISettingsStore>(),
            provider.GetService<Kazo.Jarvis.Kernel.Secrets.ISecretVault>(),
            provider.GetService<IWebSearchApi>(),
            provider.GetService<IReadingBrowser>())));

        // After the allowed folders (47): what the web tools read and send.
        services.AddSingleton(new SettingsPage(PageKey, typeof(WebToolsPage), typeof(WebToolsPage), "Title", Order: 48));
        return services;
    }
}
