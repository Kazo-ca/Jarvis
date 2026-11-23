using Kazo.Jarvis.Kernel.Llm;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tasks;
using Kazo.Jarvis.Kernel.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.ClaudeCode.Announcements;

/// <summary>Registration of the announcements of the Claude Code sessions (US2.12, US2.13; ADR 0021, sections 9 and 10).</summary>
public static class ClaudeAnnouncementsSlice
{
    /// <summary>Key of the page in the settings window.</summary>
    public const string PageKey = "ClaudeCode.Announcements";

    /// <summary>
    /// Adds the announcer, which watches the board of the tasks and publishes what Jarvis says (ADR 0024), the
    /// keeper of the silence, the tools <c>start_quiet_period</c> and <c>get_claude_sessions</c>, and the page.
    /// Registered after the slice that starts the sessions: at start, that one marks the sessions lost when
    /// Jarvis closed, and those are not news.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddClaudeAnnouncementsSlice(this IServiceCollection services)
    {
        if (services.Any(known => known.ServiceType == typeof(SessionAnnouncer)))
        {
            return services;
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<QuietTime>();
        services.AddSingleton(provider => new SessionAnnouncer(
            provider.GetRequiredService<ITaskBoard>(),
            provider.GetRequiredService<ISettingsStore>(),
            provider.GetRequiredService<QuietTime>(),
            provider.GetRequiredService<IAssistantLanguageReader>(),
            provider.GetRequiredService<IMessageBus>(),
            provider.GetService<IAssistantModel>(),
            provider.GetService<ILogger<SessionAnnouncer>>()));
        services.AddHostedService(provider => provider.GetRequiredService<SessionAnnouncer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, QuietPeriodTool>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, SessionStatusTool>());

        // After the tasks page (54): what Jarvis says of the sessions.
        services.AddSingleton(new SettingsPage(PageKey, typeof(ClaudeAnnouncementsPage), typeof(ClaudeAnnouncementsPage), "Title", Order: 55));
        return services;
    }
}
