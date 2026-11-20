using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Setup.FirstRun;

/// <summary>Registration of the first-run wizard slice (US1.4, stage 1 plan, work item 14).</summary>
public static class FirstRunSlice
{
    /// <summary>
    /// The steps of the wizard, as keys of settings pages (ADR 0002, section 3: the wizard knows the pages only through
    /// their registration). A key no slice registers is skipped. Language first: it changes every other step; models
    /// before the language model, whose advice they give.
    /// </summary>
    public static IReadOnlyList<string> StepKeys { get; } = ["Settings.Language", "Audio.Devices", "Setup.Models", "Llm.Providers"];

    /// <summary>Adds the launcher, which opens the wizard at a first run or on request, and the state of its window.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddFirstRunSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<FirstRunWizardState>();
        services.TryAddSingleton<FirstRunLauncher>();
        services.AddHostedService(provider => provider.GetRequiredService<FirstRunLauncher>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<FirstRunWizardRequested>, FirstRunLauncher>(
            provider => provider.GetRequiredService<FirstRunLauncher>()));
        return services;
    }
}
