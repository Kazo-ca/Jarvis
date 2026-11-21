using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Assistant;

/// <summary>
/// Something to tell the user that nobody asked for (ADR 0024): the end of a Claude Code session, a wait for a
/// permission. Published by the slice that has it to say, which alone decides whether it is said aloud (its
/// settings, the quiet hours). The conversation slice writes it in the conversation at once and, when
/// <paramref name="Aloud"/> is set, has it said once the assistant is at rest. The text is never sent to a model.
/// </summary>
/// <param name="Text">The sentence, in <paramref name="Language"/>.</param>
/// <param name="Language">The language the text is written in.</param>
/// <param name="Aloud">Whether it is said; otherwise it is only written.</param>
public sealed record AnnouncementToSay(string Text, AssistantLanguage Language, bool Aloud) : IEvent;
