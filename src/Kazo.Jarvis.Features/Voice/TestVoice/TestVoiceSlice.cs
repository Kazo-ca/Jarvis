using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Voice.TestVoice;

/// <summary>Registration of the voices page (US1.11).</summary>
public static class TestVoiceSlice
{
    /// <summary>Key of the voices page in the settings window.</summary>
    public const string PageKey = "Voice.TestVoice";

    /// <summary>Adds the page and its logic. The voice selection comes from the speech slice, through the kernel.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddTestVoiceSlice(this IServiceCollection services)
    {
        // One trial per page: a sentence of one window never stops the one of another.
        services.TryAddTransient<VoiceTrial>();

        // After the language (0), the orb (5), the language models (10) and the audio devices (20).
        services.AddSingleton(new SettingsPage(PageKey, typeof(VoicesSettingsPage), typeof(VoicesSettingsPage), "Title", Order: 25));
        return services;
    }
}
