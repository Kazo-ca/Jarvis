using System.Runtime.Versioning;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Adapters.Secrets;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Adapters.Storage;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Adapters;

/// <summary>Registers the real implementations of the kernel contracts.</summary>
public static class AdaptersServiceCollectionExtensions
{
    /// <summary>
    /// Adds every adapter to <paramref name="services"/>. Only the Windows host calls it, before
    /// <c>AddJarvisFeatures</c>, so that slices find their contracts (ADR 0002, section 6).
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="paths">Where the files go; <c>%USERPROFILE%\.kazo\jarvis</c> when omitted (ADR 0004).</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJarvisAdapters(this IServiceCollection services, JarvisPaths? paths = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(paths ?? JarvisPaths.ForCurrentUser());

        services.TryAddSingleton<ISettingsStore>(provider => new JsonFileSettingsStore(
            provider.GetRequiredService<JarvisPaths>(),
            provider.GetServices<ISettingsMigration>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetService<ILogger<JsonFileSettingsStore>>()));

        services.TryAddSingleton<IDocumentStore>(provider => new SqliteDocumentStore(
            provider.GetRequiredService<JarvisPaths>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetService<ILogger<SqliteDocumentStore>>()));

        // Both vaults need Windows (Credential Manager, DPAPI). The Windows host is the only one to load Adapters.
        if (OperatingSystem.IsWindows())
        {
            AddWindowsVaults(services);
        }

        return services;
    }

    /// <summary>The selected vault over the Credential Manager and the DPAPI file.</summary>
    [SupportedOSPlatform("windows")]
    private static void AddWindowsVaults(IServiceCollection services)
    {
        services.TryAddSingleton(provider => new SelectedSecretVault(
            provider.GetRequiredService<ISettingsStore>(),
            new Dictionary<VaultKind, ISecretVault>
            {
                [VaultKind.CredentialManager] = new CredentialManagerVault(),
                [VaultKind.EncryptedFile] = new EncryptedFileVault(
                    provider.GetRequiredService<JarvisPaths>().SecretsFile, new DpapiSecretProtector()),
            },
            provider.GetService<ILogger<SelectedSecretVault>>()));
        services.TryAddSingleton<ISecretVault>(provider => provider.GetRequiredService<SelectedSecretVault>());
        services.TryAddSingleton<ISecretVaultSelector>(provider => provider.GetRequiredService<SelectedSecretVault>());
    }
}
