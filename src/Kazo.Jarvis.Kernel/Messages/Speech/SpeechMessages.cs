using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Speech;

/// <summary>
/// A sentence of the answer to say aloud (US1.9). Published by the conversation (work item 13) as soon as each
/// sentence is complete, in order. The <c>Voice/Speech</c> slice synthesizes and plays them one after the other,
/// publishes <see cref="Assistant.FirstSentenceReady"/> when the first one starts playing, and
/// <see cref="Assistant.SpeechEnded"/> after the last one.
/// </summary>
/// <param name="Turn">The turn answered.</param>
/// <param name="Text">The sentence.</param>
public sealed record SentenceToSpeak(TurnId Turn, string Text) : IEvent;

/// <summary>
/// The answer of <paramref name="Turn"/> has no more sentences. Once the queue is played, the speech slice
/// publishes <see cref="Assistant.SpeechEnded"/>; if nothing was said, <see cref="Assistant.AnswerFinishedWithoutSpeech"/>.
/// </summary>
/// <param name="Turn">The turn answered.</param>
public sealed record AnswerSentencesEnded(TurnId Turn) : IEvent;
