using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Tasks.Fan;

/// <summary>Registration of the task fan (ADR 0010; stage 2 plan, work item 14).</summary>
public static class TaskFanSlice
{
    /// <summary>
    /// Adds the fan, which answers the double click on the orb. The host draws it by placing
    /// <see cref="TaskFanView"/> on its fan surface; a host without a surface has no fan.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddTaskFanSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<TaskFan>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<TaskFanToggleRequested>, TaskFan>(
            provider => provider.GetRequiredService<TaskFan>()));
        return services;
    }
}
