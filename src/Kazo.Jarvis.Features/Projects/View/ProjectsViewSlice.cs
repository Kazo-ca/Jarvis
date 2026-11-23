using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Projects.View;

/// <summary>
/// The projects view (ADR 0026): the projects, their subjects and their tasks over the whole screen, opened by a
/// double click on the orb. It reads the task board and the catalog of projects through the kernel.
/// </summary>
public static class ProjectsViewSlice
{
    /// <summary>Registers the slice. A host with no window service must not call it: the orb then has no double click.</summary>
    /// <param name="services">The services of the host.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddProjectsViewSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ProjectsViewState>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<ProjectsViewRequested>, ProjectsViewOpener>());
        return services;
    }
}
