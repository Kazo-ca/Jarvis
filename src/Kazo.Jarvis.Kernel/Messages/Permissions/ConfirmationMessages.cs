using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Permissions;

namespace Kazo.Jarvis.Kernel.Messages.Permissions;

/// <summary>A confirmation request opened: sets the condition "confirmation pending" (ADR 0023, section 1).</summary>
/// <param name="Id">The identifier of the request.</param>
/// <param name="Class">Its class.</param>
public sealed record ConfirmationOpened(Guid Id, ActionClass Class) : AssistantInput;

/// <summary>A confirmation request closed, whatever its answer: clears the condition.</summary>
/// <param name="Id">The identifier of the request.</param>
public sealed record ConfirmationClosed(Guid Id) : AssistantInput;

/// <summary>
/// The question of a request born in a spoken turn, to say aloud (ADR 0023, section 2). The speech slice says it in
/// the voice of the turn, without entering the "Speaking" phase, then publishes <see cref="ConfirmationQuestionSaid"/>.
/// </summary>
/// <param name="Id">The identifier of the request.</param>
/// <param name="Turn">The turn the request was born in.</param>
/// <param name="Text">The summary followed by the question.</param>
/// <param name="Listen">Whether a spoken answer is accepted: "Sensitive" class, microphone not muted.</param>
public sealed record ConfirmationQuestionToSay(Guid Id, TurnId Turn, string Text, bool Listen) : IEvent;

/// <summary>The question was said to the end. When <paramref name="Listen"/> is set, the transcription slice opens its single listening.</summary>
/// <param name="Id">The identifier of the request.</param>
/// <param name="Turn">The turn the request was born in.</param>
/// <param name="Listen">Whether a spoken answer is accepted.</param>
public sealed record ConfirmationQuestionSaid(Guid Id, TurnId Turn, bool Listen) : IEvent;

/// <summary>
/// What was heard during the single listening that follows the question. It goes to the confirmation slice only,
/// never to a model (ADR 0023, section 3).
/// </summary>
/// <param name="Id">The identifier of the request.</param>
/// <param name="Text">The transcription, or <see langword="null"/> when nothing was heard: the request stays open.</param>
public sealed record ConfirmationAnswerHeard(Guid Id, string? Text) : IEvent;
