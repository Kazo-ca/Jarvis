using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Setup.Models;

/// <summary>Registration of the models slice (stage 1 plan, work item 14, ADR 0006, section 7).</summary>
public static class ModelsSlice
{
    /// <summary>Key of the models page in the settings window and in the first-run wizard.</summary>
    public const string PageKey = "Setup.Models";

    /// <summary>
    /// Adds the keeper of the downloads and the models page. The host provides
    /// <see cref="Kernel.Models.IModelInstaller"/> and <see cref="Kernel.Hardware.IGraphicsCardProbe"/>.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddModelsSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<ModelsKeeper>();

        // After the voices page (35): the files of the whole voice chain.
        services.AddSingleton(new SettingsPage(PageKey, typeof(ModelsSettingsPage), typeof(ModelsSettingsPage), "Title", Order: 40));
        return services;
    }
}
