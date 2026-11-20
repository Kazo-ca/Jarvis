using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Storage;
using Kazo.Jarvis.Kernel.Updates;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>Registers the simulated contracts of the Web host (ADR 0003, section 3).</summary>
public static class SimulatedServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory settings, document store and vault, and the simulated desktop services (orb, dialogs,
    /// hotkey, start with Windows), the Windows display language, and the simulated release source and updater launcher. Called before <c>AddJarvisFeatures</c>.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJarvisSimulatedServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISettingsStore, InMemorySettingsStore>();
        services.TryAddSingleton<IDocumentStore>(provider => new InMemoryDocumentStore(provider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<InMemorySecretVault>();
        services.TryAddSingleton<ISecretVault>(provider => provider.GetRequiredService<InMemorySecretVault>());
        services.TryAddSingleton<ISecretVaultSelector>(provider => provider.GetRequiredService<InMemorySecretVault>());

        services.TryAddSingleton<SimulatedOrbWindow>();
        services.TryAddSingleton<IOrbWindow>(provider => provider.GetRequiredService<SimulatedOrbWindow>());
        services.TryAddSingleton<OverlayDialogService>();
        services.TryAddSingleton<IDialogService>(provider => provider.GetRequiredService<OverlayDialogService>());
        services.TryAddSingleton<SimulatedGlobalHotkey>();
        services.TryAddSingleton<IGlobalHotkey>(provider => provider.GetRequiredService<SimulatedGlobalHotkey>());
        services.TryAddSingleton<ILaunchAtLogin, InMemoryLaunchAtLogin>();
        services.TryAddSingleton<ISystemLanguage, SimulatedSystemLanguage>();

        services.TryAddSingleton<SimulatedReleaseSource>();
        services.TryAddSingleton<IReleaseSource>(provider => provider.GetRequiredService<SimulatedReleaseSource>());
        services.TryAddSingleton<SimulatedUpdateLauncher>();
        services.TryAddSingleton<IUpdateLauncher>(provider => provider.GetRequiredService<SimulatedUpdateLauncher>());
        return services;
    }
}
