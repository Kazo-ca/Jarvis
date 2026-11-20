using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Localization;

/// <summary>
/// The language of the assistant changed (ADR 0008, section 1). Published after
/// <see cref="IAssistantLanguageReader.Current"/> holds the new language: open windows reload their texts,
/// transcription and voice change at the next turn, without a restart.
/// </summary>
/// <param name="Previous">The language before the change.</param>
/// <param name="Current">The language now in use.</param>
public sealed record AssistantLanguageChanged(AssistantLanguage Previous, AssistantLanguage Current) : IEvent;
