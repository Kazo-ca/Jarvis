using Kazo.Jarvis.Kernel.Agents;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Agents.Catalog;

/// <summary>Registration of the agents (ADR 0025, sections 1 to 4): their files, the two tools of the hand and the « Agents » page.</summary>
public static class AgentsSlice
{
    /// <summary>Key of the agents page in the settings window.</summary>
    public const string PageKey = "Agents.Catalog";

    /// <summary>
    /// Adds the catalog of the agents, read by the conversation through <see cref="IAgentCatalog"/>, the tools
    /// <c>hand_to_agent</c> and <c>return_to_jarvis</c>, and the page. The folder of the files comes from the host
    /// (<see cref="IAgentFiles"/>); without it there is no agent, and a turn gives every enabled tool to the model.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddAgentsSlice(this IServiceCollection services)
    {
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IAgentFiles)))
        {
            return services;
        }

        services.TryAddSingleton<AgentCatalog>();
        services.TryAddSingleton<IAgentCatalog>(provider => provider.GetRequiredService<AgentCatalog>());
        services.AddHostedService(provider => provider.GetRequiredService<AgentCatalog>());

        // The catalog reads the registry these tools are in: it is asked when a tool is described, not when it is built.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, HandToAgentTool>(provider => new HandToAgentTool(provider.GetRequiredService<AgentCatalog>, provider.GetService<Kazo.Jarvis.Kernel.Projects.IProjectCatalog>)));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, ReturnToJarvisTool>());

        // Right after the memory (16): who answers, before what the tools may do.
        services.AddSingleton(new SettingsPage(PageKey, typeof(AgentsPage), typeof(AgentsPage), "Title", Order: 17));
        return services;
    }
}
