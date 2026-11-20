using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Voice.WakeWord;

/// <summary>Registration of the wake word slice (US1.5, ADR 0006, section 2).</summary>
public static class WakeWordSlice
{
    /// <summary>Key of the wake word page in the settings window.</summary>
    public const string PageKey = "Voice.WakeWord";

    /// <summary>
    /// Adds the listener and the settings page. The host provides <see cref="Kernel.Voice.IWakeWordEngine"/>;
    /// the microphone stream comes from the kernel, filled by the audio slice.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddWakeWordSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<WakeWordListener>();
        services.AddHostedService(provider => provider.GetRequiredService<WakeWordListener>());

        // Right after the audio page (20): the score moves with the microphone chosen there.
        services.AddSingleton(new SettingsPage(PageKey, typeof(WakeWordSettingsPage), typeof(WakeWordSettingsPage), "Title", Order: 25));
        return services;
    }
}
