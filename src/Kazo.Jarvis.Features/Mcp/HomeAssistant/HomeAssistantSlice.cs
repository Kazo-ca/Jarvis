using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools.Servers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Mcp.HomeAssistant;

/// <summary>
/// Slice of Home Assistant (ADR 0018, sections 5 and 7; US2.3): the classification of the calls to its MCP server
/// from the devices they aim at, and the « sécurité » list. It never references the slice of the servers: it
/// registers its classification through a kernel contract.
/// </summary>
public static class HomeAssistantSlice
{
    /// <summary>The key of the page in the settings window.</summary>
    public const string PageKey = "Mcp.HomeAssistant";

    /// <summary>Registers the slice.</summary>
    /// <param name="services">The services of the host.</param>
    /// <returns>The same services.</returns>
    public static IServiceCollection AddHomeAssistantSlice(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IToolServerClassifier, HomeAssistantClassifier>(provider =>
            new HomeAssistantClassifier(provider.GetRequiredService<ISettingsStore>(), provider.GetService<TimeProvider>())));

        // After the tool servers (49): what is security home automation.
        services.AddSingleton(new SettingsPage(PageKey, typeof(HomeAssistantPage), typeof(HomeAssistantPage), "Title", Order: 50));
        return services;
    }
}
