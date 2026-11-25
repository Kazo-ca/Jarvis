using Kazo.Jarvis.Kernel.Storage;
using Kazo.Jarvis.Kernel.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Tasks.Journal;

/// <summary>The slice that keeps the log of the tasks (ADR 0026, section 5).</summary>
public static class TaskLogSlice
{
    /// <summary>Registers the log of the tasks.</summary>
    /// <param name="services">The services of the host.</param>
    /// <returns>The same services.</returns>
    public static IServiceCollection AddTaskLogSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<ITaskLog>(provider => new TaskLog(
            provider.GetRequiredService<IDocumentStore>(),
            provider.GetService<ITaskBoard>(),
            provider.GetService<ILogger<TaskLog>>()));
        return services;
    }
}
