using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Voice;

/// <summary>
/// What the user said in a spoken turn (US1.7), published by the transcription slice after
/// <c>UtteranceEnded</c>, for the conversation (work item stage1/13-conversation). Never published for a
/// cancelled turn.
/// </summary>
/// <param name="Turn">The turn listened to.</param>
/// <param name="Text">
/// The request, trimmed and without Whisper's markers. Empty when Whisper heard no words: the conversation then
/// ends the turn without an answer.
/// </param>
/// <param name="Language">The language it was transcribed in (the language of the assistant, ADR 0008).</param>
/// <param name="Speech">Length of the audio given to Whisper.</param>
/// <param name="Elapsed">Time spent transcribing, from the end of the utterance.</param>
public sealed record UtteranceTranscribed(TurnId Turn, string Text, AssistantLanguage Language, TimeSpan Speech, TimeSpan Elapsed) : IEvent;
