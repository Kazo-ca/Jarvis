using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Voice.Transcription;

/// <summary>Registration of the transcription slice (US1.7, ADR 0006, sections 3 and 4).</summary>
public static class TranscriptionSlice
{
    /// <summary>Key of the transcription page in the settings window.</summary>
    public const string PageKey = "Voice.Transcription";

    /// <summary>
    /// Adds the Whisper model keeper, the listener of spoken turns and the settings page. The host provides
    /// <see cref="Kernel.Voice.ISpeechRecognition"/> and, optionally, <see cref="Kernel.Voice.IVoiceActivityDetection"/>
    /// (Silero); without it the end of speech uses the energy threshold.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddTranscriptionSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<RecognizerKeeper>();
        services.AddHostedService(provider => provider.GetRequiredService<RecognizerKeeper>());

        services.TryAddSingleton<UtteranceListener>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<TurnStarted>, UtteranceListener>(
            provider => provider.GetRequiredService<UtteranceListener>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<TurnCancelled>, UtteranceListener>(
            provider => provider.GetRequiredService<UtteranceListener>()));

        // After the audio page (20).
        services.AddSingleton(new SettingsPage(PageKey, typeof(TranscriptionSettingsPage), typeof(TranscriptionSettingsPage), "Title", Order: 25));
        return services;
    }
}
