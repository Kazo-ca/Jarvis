using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Messaging;

namespace Kazo.Jarvis.Kernel.Messages.Permissions;

/// <summary>
/// What Jarvis says aloud for a request born in a turn (ADR 0023, section 2): the summary and the question after a
/// spoken request, the announcement that an interaction is required after a typed one. The speech slice says it in
/// the voice of the turn, without entering the "Speaking" phase, then publishes <see cref="ConfirmationQuestionSaid"/>.
/// </summary>
/// <param name="Id">The identifier of the request.</param>
/// <param name="Turn">The turn the request was born in.</param>
/// <param name="Text">The sentence to say.</param>
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
