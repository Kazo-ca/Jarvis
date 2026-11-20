using Kazo.Jarvis.Features.Desktop.Hotkey;
using Kazo.Jarvis.Features.Desktop.LaunchAtLogin;
using Kazo.Jarvis.Features.Maintenance.Updates;
using Kazo.Jarvis.Features.Settings.Language;
using Kazo.Jarvis.Features.Settings.Window;
using Kazo.Jarvis.Features.Shared;
using Kazo.Jarvis.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Kazo.Jarvis.Features;

/// <summary>Registers every slice (ADR 0002, section 6).</summary>
public static class FeaturesServiceCollectionExtensions
{
    /// <summary>
    /// Adds every slice to <paramref name="services"/>. Each slice exposes its own registration
    /// method; this one calls them all. Hosts call it after registering the contracts.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJarvisFeatures(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The slices talk through the kernel's bus and read its state machine (ADR 0002, section 5).
        services.AddJarvisKernel();

        // Texts in the language of the assistant, for every slice (ADR 0008).
        services.AddJarvisShared();

        // Desktop slices (stage1/4-hosts): their contracts come from the host.
        services.AddHotkeySlice();
        services.AddLaunchAtLoginSlice();

        // Settings slices (stage1/5-shared-ui-language): the language, and the window that shows the pages.
        services.AddLanguageSlice();
        services.AddSettingsWindowSlice();

        // Updates (stage1/16-updates): release source and updater launcher come from the host.
        services.AddUpdatesSlice();

        // Other slices arrive with their work items (stage1/6-orb onwards).
        return services;
    }
}
