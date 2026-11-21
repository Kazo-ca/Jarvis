using Kazo.Jarvis.Kernel.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        return services;
    }
}
