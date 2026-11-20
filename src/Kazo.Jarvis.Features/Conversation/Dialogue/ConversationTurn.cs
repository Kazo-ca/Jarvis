using System.Text.Json.Serialization;
using Kazo.Jarvis.Kernel.Messages.Assistant;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>How a turn of the conversation ended.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TurnOutcome>))]
public enum TurnOutcome
{
    /// <summary>The model is answering, or the answer is being said.</summary>
    Pending,

    /// <summary>The answer was given in full.</summary>
    Answered,

    /// <summary>The user interrupted Jarvis (US1.12); <see cref="ConversationTurn.Answer"/> holds what was written by then.</summary>
    Interrupted,

    /// <summary>The model or the voice failed; <see cref="ConversationTurn.Failure"/> names the cause.</summary>
    Failed,
}

/// <summary>
/// One turn of the conversation (US1.14), as the window shows it and as the collection <c>Conversation.Turns</c>
/// keeps it (ADR 0004, section 3), with what is recorded at each answer (ADR 0007, section 7).
/// </summary>
public sealed record ConversationTurn
{
    /// <summary>Identity of the turn in this slice; the turn numbers of the state machine start again at each launch.</summary>
    public Guid Key { get; init; } = Guid.NewGuid();

    /// <summary>When the request was made.</summary>
    public DateTimeOffset At { get; init; }

    /// <summary>Spoken or typed; a typed request gets a written answer only.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<TurnInput>))]
    public TurnInput Input { get; init; }

    /// <summary>The request, as heard or typed.</summary>
    public string Request { get; init; } = string.Empty;

    /// <summary>The answer as written by the model; partial for an interrupted or failed turn.</summary>
    public string Answer { get; init; } = string.Empty;

    /// <summary>Code of the language of the turn (<c>fr</c>, <c>en</c>).</summary>
    public string Language { get; init; } = string.Empty;

    /// <summary>Name of the provider that answered, as shown to the user.</summary>
    public string? Provider { get; init; }

    /// <summary>The model that answered, as the server named it.</summary>
    public string? Model { get; init; }

    /// <summary>Tokens sent, when the provider says.</summary>
    public long? InputTokens { get; init; }

    /// <summary>Tokens written, when the provider says.</summary>
    public long? OutputTokens { get; init; }

    /// <summary>Time the model took, from the request sent to the last token.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// For a spoken turn, from the end of the utterance to the first sound of the answer (target under 2 s, stage 1
    /// plan, work item 13). Measured on the live turn; absent from a turn stored before its first sound.
    /// </summary>
    public TimeSpan? FirstSound { get; init; }

    /// <summary>How the turn ended.</summary>
    public TurnOutcome Outcome { get; init; }

    /// <summary>For a failed turn, the cause (an <c>LlmFailure</c> or a <c>FaultCause</c> name). Never a secret.</summary>
    public string? Failure { get; init; }
}
