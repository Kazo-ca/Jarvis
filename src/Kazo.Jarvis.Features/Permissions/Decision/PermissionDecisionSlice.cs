using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Permissions.Decision;

/// <summary>Registration of the decision point of the permissions (ADR 0009 and ADR 0017, section 2).</summary>
public static class PermissionDecisionSlice
{
    /// <summary>Key of the permission log page in the settings window.</summary>
    public const string PageKey = "Permissions.Decision";

    /// <summary>Adds the decision point, its log and the page that shows it. The store comes from the host.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddPermissionDecisionSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPermissionDecision, PermissionDecisionPoint>();

        // Last page of the settings: the log of what was decided (ADR 0023, section 5).
        services.AddSingleton(new SettingsPage(PageKey, typeof(DecisionLogPage), typeof(DecisionLogPage), "Title", Order: 50));
        return services;
    }
}
