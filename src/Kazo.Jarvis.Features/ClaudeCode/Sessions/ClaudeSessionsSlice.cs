using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.ClaudeCode.Sessions;

/// <summary>Registration of the Claude Code sessions slice (US2.11, US2.14; ADR 0021, sections 6 and 10).</summary>
public static class ClaudeSessionsSlice
{
    /// <summary>Key of the sessions page in the settings window.</summary>
    public const string PageKey = "ClaudeCode.Sessions";

    /// <summary>
    /// Adds the list of the sessions, read from <see cref="Kernel.ClaudeCode.IClaudeSessionRegistry"/> (a native
    /// service of the host), and its page.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddClaudeSessionsSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ClaudeSessionList>();
        services.AddHostedService(provider => provider.GetRequiredService<ClaudeSessionList>());

        // After the tool servers and Home Assistant (49, 50): another program Jarvis works with.
        services.AddSingleton(new SettingsPage(PageKey, typeof(ClaudeSessionsPage), typeof(ClaudeSessionsPage), "Title", Order: 52));
        return services;
    }
}
