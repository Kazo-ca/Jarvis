using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Projects;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Projects.Catalog;

/// <summary>Registration of the projects slice (ADR 0025, section 6).</summary>
public static class ProjectsSlice
{
    /// <summary>The key of the « Projets » page.</summary>
    public const string PageKey = "Projects.Catalog";

    /// <summary>Adds the projects: their catalog and their page.</summary>
    /// <param name="services">The services of the host.</param>
    /// <returns>The same services.</returns>
    public static IServiceCollection AddProjectsSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(provider => new ProjectCatalog(provider.GetRequiredService<ISettingsStore>(), provider.GetService<ILocalFiles>()));
        services.TryAddSingleton<IProjectCatalog>(provider => provider.GetRequiredService<ProjectCatalog>());

        // Right after the agents (17): what the « Projet » agent is loaded with.
        services.AddSingleton(new SettingsPage(PageKey, typeof(ProjectsPage), typeof(ProjectsPage), "Title", Order: 18));
        return services;
    }
}
