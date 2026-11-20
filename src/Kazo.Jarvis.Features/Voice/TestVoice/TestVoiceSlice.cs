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

        // Last of the voice chain: after the wake word (25) and the transcription (30); the mouth after the ears.
        services.AddSingleton(new SettingsPage(PageKey, typeof(VoicesSettingsPage), typeof(VoicesSettingsPage), "Title", Order: 35));
        return services;
    }
}
