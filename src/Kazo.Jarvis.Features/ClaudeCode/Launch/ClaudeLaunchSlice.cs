using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Projects;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.ClaudeCode.Launch;

/// <summary>Registration of the slice that starts Claude Code sessions (US2.9, US2.10; ADR 0021, sections 1, 4, 5, 8 and 10).</summary>
public static class ClaudeLaunchSlice
{
    /// <summary>Key of the page in the settings window.</summary>
    public const string PageKey = "ClaudeCode.Launch";

    /// <summary>
    /// Adds the runner of the sessions, which gives <see cref="IClaudeTasks"/> to the conversation and
    /// <see cref="IClaudeApprovals"/> to the local server, and its page. Claude Code itself
    /// (<see cref="IClaudeCode"/>) and the allowed folders come from the host and from the file tools; without
    /// them the runner says why nothing starts.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddClaudeLaunchSlice(this IServiceCollection services)
    {
        if (services.Any(known => known.ServiceType == typeof(ClaudeTaskRunner)))
        {
            return services;
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(provider => new ClaudeTaskRunner(
            provider.GetRequiredService<ITaskBoard>(),
            provider.GetRequiredService<IPermissionDecision>(),
            provider.GetRequiredService<ISettingsStore>(),
            provider.GetRequiredService<IMessageBus>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetService<IClaudeCode>(),
            provider.GetService<IAllowedFolders>(),
            provider.GetService<ISpendCap>(),
            provider.GetService<ILogger<ClaudeTaskRunner>>(),
            provider.GetService<IProjectCatalog>()));
        services.TryAddSingleton<IClaudeTasks>(Runner);
        services.TryAddSingleton<IClaudeApprovals>(Runner);
        services.AddHostedService(Runner);

        // After the hooks page (53): the sessions Jarvis starts itself.
        services.AddSingleton(new SettingsPage(PageKey, typeof(ClaudeLaunchPage), typeof(ClaudeLaunchPage), "Title", Order: 54));
        return services;
    }

    private static ClaudeTaskRunner Runner(IServiceProvider provider) => provider.GetRequiredService<ClaudeTaskRunner>();
}
