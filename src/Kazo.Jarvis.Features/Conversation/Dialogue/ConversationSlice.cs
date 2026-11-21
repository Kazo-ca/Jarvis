using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Desktop;
using Kazo.Jarvis.Kernel.Messages.Voice;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>Registration of the conversation slice (US1.9, US1.12, US1.14, US1.15, ADR 0005, 0007 and 0008).</summary>
public static class ConversationSlice
{
    /// <summary>Key of the personality page in the settings window.</summary>
    public const string PageKey = "Conversation.Personality";

    /// <summary>
    /// Adds the engine of the turns, the journal of the conversation, its window and the personality page. The
    /// default model comes from the language models slice through <see cref="Kernel.Llm.IAssistantModel"/>; the
    /// store of the turns from the host.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddConversationSlice(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SystemPrompt>();
        services.TryAddSingleton<ConversationJournal>();

        services.TryAddSingleton<ConversationEngine>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<TurnStarted>, ConversationEngine>(Engine));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<UtteranceEnded>, ConversationEngine>(Engine));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<UtteranceTranscribed>, ConversationEngine>(Engine));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<FirstSentenceReady>, ConversationEngine>(Engine));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<SpeechEnded>, ConversationEngine>(Engine));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<AnswerFinishedWithoutSpeech>, ConversationEngine>(Engine));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<TurnCancelled>, ConversationEngine>(Engine));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<TurnFailed>, ConversationEngine>(Engine));

        // What Jarvis says without being asked (ADR 0024).
        services.TryAddSingleton<AnnouncementDesk>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<AnnouncementToSay>, AnnouncementDesk>(Desk));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<AssistantStateChanged>, AnnouncementDesk>(Desk));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<TurnStarted>, AnnouncementDesk>(Desk));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventHandler<ConversationWindowRequested>, ConversationWindowOpener>());

        // Right after the language models (10): the personality is what the model is told first.
        services.AddSingleton(new SettingsPage(PageKey, typeof(PersonalitySettingsPage), typeof(PersonalitySettingsPage), "Title", Order: 15));
        return services;
    }

    private static ConversationEngine Engine(IServiceProvider provider) => provider.GetRequiredService<ConversationEngine>();

    private static AnnouncementDesk Desk(IServiceProvider provider) => provider.GetRequiredService<AnnouncementDesk>();
}
