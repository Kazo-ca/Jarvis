using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Kernel.Tools.Servers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Mcp.Servers;

/// <summary>
/// Slice of the tool servers (ADR 0018, section 7; US2.2): the servers of the user, their page, the connection and
/// the activation of their tools one by one.
/// </summary>
public static class McpServersSlice
{
    /// <summary>The key of the page in the settings window.</summary>
    public const string PageKey = "Mcp.Servers";

    /// <summary>Registers the slice.</summary>
    /// <param name="services">The services of the host.</param>
    /// <returns>The same services.</returns>
    public static IServiceCollection AddMcpServersSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(provider => new McpServerHub(
            provider.GetRequiredService<ISettingsStore>(),
            provider.GetRequiredService<IToolRegistry>(),
            provider.GetService<ISecretVault>(),
            provider.GetService<IToolServerConnector>(),
            provider.GetService<TimeProvider>(),
            provider.GetService<ILogger<McpServerHub>>(),
            provider.GetServices<IToolServerClassifier>()));
        services.AddHostedService(provider => provider.GetRequiredService<McpServerHub>());

        // After the web tools (48): where more tools come from.
        services.AddSingleton(new SettingsPage(PageKey, typeof(McpServersPage), typeof(McpServersPage), "Title", Order: 49));
        return services;
    }
}
