using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Conversation;

/// <summary>
/// A turn of the conversation was stored (ADR 0019, section 7). It carries what the user said or typed and nothing
/// else: neither the answer nor the result of a tool, so that a text read from outside has no way to a listener
/// that only trusts the user.
/// </summary>
/// <param name="Turn">The turn of the state machine.</param>
/// <param name="Key">The identity of the stored turn.</param>
/// <param name="At">When the request was made.</param>
/// <param name="Request">The words of the user.</param>
/// <param name="Language">The language of the turn.</param>
public sealed record TurnRecorded(TurnId Turn, Guid Key, DateTimeOffset At, string Request, AssistantLanguage Language) : IEvent;

/// <summary>An answered exchange of the past: the request of the user and the answer of Jarvis, without the result of the tools.</summary>
/// <param name="Key">The identity of the stored turn.</param>
/// <param name="At">When the request was made.</param>
/// <param name="Request">The words of the user.</param>
/// <param name="Answer">The answer of Jarvis.</param>
public sealed record PastExchange(Guid Key, DateTimeOffset At, string Request, string Answer);

/// <summary>
/// At the start of a turn, the answered exchanges that are no longer sent to the model word for word, because of
/// their number, of their size or of their age (ADR 0019, section 5). The same exchange is announced at every turn
/// as long as it is in the memory of the conversation: the listener keeps track of what it already took.
/// </summary>
/// <param name="Exchanges">The most recent exchanges that left the history, oldest first.</param>
/// <param name="Language">The language of the turn that starts.</param>
public sealed record ExchangesLeftHistory(IReadOnlyList<PastExchange> Exchanges, AssistantLanguage Language) : IEvent;
