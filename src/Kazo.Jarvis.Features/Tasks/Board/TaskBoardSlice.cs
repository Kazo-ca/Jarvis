using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.LocalServer;
using Kazo.Jarvis.Kernel.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Tasks.Board;

/// <summary>Registration of the board of the tasks (ADR 0013, section 6; ADR 0021, sections 7 and 10).</summary>
public static class TaskBoardSlice
{
    /// <summary>Adds the board, which gives <see cref="ITaskBoard"/> to whoever starts, follows or shows a task.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddTaskBoardSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<TaskBoard>();
        services.TryAddSingleton<ITaskBoard>(provider => provider.GetRequiredService<TaskBoard>());
        services.AddHostedService(provider => provider.GetRequiredService<TaskBoard>());
        if (services.All(known => known.ServiceType != typeof(TaskCount)))
        {
            // After the board: it reads the tasks the board loaded.
            services.AddSingleton<TaskCount>();
            services.AddHostedService(provider => provider.GetRequiredService<TaskCount>());
        }

        // The routes of the hooks (ADR 0021, section 3): the local server of the host finds them as ILocalRoute.
        if (services.Any(known => known.ServiceType == typeof(TaskHooks)))
        {
            return services;
        }

        services.TryAddSingleton(provider => new TaskHooks(
            provider.GetRequiredService<ITaskBoard>(),
            provider.GetService<IClaudeSessionRegistry>(),
            provider.GetService<ILogger<TaskHooks>>()));
        foreach (var kind in Enum.GetValues<TaskHookKind>())
        {
            services.AddSingleton<ILocalRoute>(provider => new TaskHookRoute(kind, provider.GetRequiredService<TaskHooks>()));
        }
        return services;
    }
}
