using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Permissions;
using Kazo.Jarvis.Kernel.Messages.Speech;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Speech;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Voice.Speech;

/// <summary>Registration of the speech slice (US1.9, US1.10, ADR 0006, section 5).</summary>
public static class SpeechSlice
{
    /// <summary>
    /// Adds the voice selection (<see cref="IVoiceSelection"/>) and the player of the answers. The host
    /// registers the engines (<see cref="ISpeechEngine"/>) and the playback.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddSpeechSlice(this IServiceCollection services)
    {
        services.TryAddSingleton<VoiceSelection>();
        services.TryAddSingleton<IVoiceSelection>(provider => provider.GetRequiredService<VoiceSelection>());

        services.TryAddSingleton<SpeechPlayer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<SentenceToSpeak>, SpeechPlayer>(
            provider => provider.GetRequiredService<SpeechPlayer>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<AnswerSentencesEnded>, SpeechPlayer>(
            provider => provider.GetRequiredService<SpeechPlayer>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<ConfirmationQuestionToSay>, SpeechPlayer>(
            provider => provider.GetRequiredService<SpeechPlayer>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<InteractionClosed>, SpeechPlayer>(
            provider => provider.GetRequiredService<SpeechPlayer>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<AnswerFinishedWithoutSpeech>, SpeechPlayer>(
            provider => provider.GetRequiredService<SpeechPlayer>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<TurnCancelled>, SpeechPlayer>(
            provider => provider.GetRequiredService<SpeechPlayer>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<TurnFailed>, SpeechPlayer>(
            provider => provider.GetRequiredService<SpeechPlayer>()));
        return services;
    }
}
