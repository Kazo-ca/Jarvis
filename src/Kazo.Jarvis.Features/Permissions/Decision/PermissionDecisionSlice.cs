using Kazo.Jarvis.Kernel.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Permissions.Decision;

/// <summary>Registration of the decision point of the permissions (ADR 0009 and ADR 0017, section 2).</summary>
public static class PermissionDecisionSlice
{
    /// <summary>Adds the decision point and its log. The store comes from the host.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddPermissionDecisionSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPermissionDecision, PermissionDecisionPoint>();
        return services;
    }
}
