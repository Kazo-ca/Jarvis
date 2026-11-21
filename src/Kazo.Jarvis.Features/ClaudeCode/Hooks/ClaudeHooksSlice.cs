using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.ClaudeCode.Hooks;

/// <summary>Registration of the Claude Code hooks slice (US2.12; ADR 0021, sections 3, 6 and 10).</summary>
public static class ClaudeHooksSlice
{
    /// <summary>Key of the hooks page in the settings window.</summary>
    public const string PageKey = "ClaudeCode.Hooks";

    /// <summary>
    /// Adds the installation of the hooks in the user's Claude Code settings, through
    /// <see cref="IClaudeUserSettings"/> (a native service of the host), and its page.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddClaudeHooksSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(provider => new ClaudeHooks(
            provider.GetRequiredService<ISettingsStore>(),
            provider.GetService<IClaudeUserSettings>(),
            provider.GetService<ISecretVault>(),
            provider.GetService<ILogger<ClaudeHooks>>()));

        // Right after the sessions page (52): the hooks are what tells Jarvis that a session waits or has ended.
        if (!services.Any(known => known.ImplementationInstance is SettingsPage { Key: PageKey }))
        {
            services.AddSingleton(new SettingsPage(PageKey, typeof(ClaudeHooksPage), typeof(ClaudeHooksPage), "Title", Order: 53));
        }

        return services;
    }
}
