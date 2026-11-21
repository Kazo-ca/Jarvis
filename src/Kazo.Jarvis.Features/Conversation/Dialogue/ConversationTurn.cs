using System.Text.Json.Serialization;
using Kazo.Jarvis.Kernel.Conversation;
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

/// <summary>How a tool call of a turn ended.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolCallOutcome>))]
public enum ToolCallOutcome
{
    /// <summary>The decision is being taken, or the tool is running.</summary>
    Running,

    /// <summary>The tool ran; <see cref="ToolCallEntry.Result"/> holds what it returned.</summary>
    Done,

    /// <summary>The decision point denied the call; <see cref="ToolCallEntry.Result"/> holds the cause told to the model.</summary>
    Denied,

    /// <summary>The tool failed or took too long.</summary>
    Failed,

    /// <summary>The user interrupted the turn during the call.</summary>
    Interrupted,
}

/// <summary>A tool call of a turn (ADR 0017, section 5): what the window shows and what is stored with the turn.</summary>
public sealed record ToolCallEntry
{
    /// <summary>The name of the tool, as the model wrote it.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The arguments, as JSON.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>What was returned to the model: the result of the tool, or the cause of the denial or of the failure.</summary>
    public string? Result { get; init; }

    /// <summary>How the call ended.</summary>
    public ToolCallOutcome Outcome { get; init; }

    /// <summary>The class the decision point gave to the call (an <c>ActionClass</c> name), when it could tell.</summary>
    public string? Class { get; init; }

    /// <summary>For a denied call, the cause (a <c>DenialCause</c> name, <c>Again</c> or <c>CallLimit</c>).</summary>
    public string? Denial { get; init; }

    /// <summary>Time taken by the decision and the tool.</summary>
    public TimeSpan? Duration { get; init; }
}

/// <summary>
/// The hand-off of a turn to a target (ADR 0020, section 5): what the window shows next to who answered, and
/// what is stored with the turn.
/// </summary>
public sealed record HandOffEntry
{
    /// <summary>A question or a task.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<HandOffKind>))]
    public HandOffKind Kind { get; init; }

    /// <summary>Why the local model handed over, in its words; absent from a forced hand-off.</summary>
    public string? Reason { get; init; }

    /// <summary>Whether the first words of the request forced it (« Demande à Claude… »).</summary>
    public bool Forced { get; init; }

    /// <summary>The name said for the target (« Claude », « ChatGPT »); absent when there was none.</summary>
    public string? Target { get; init; }

    /// <summary>Name of the local provider that handed over; absent from a forced hand-off.</summary>
    public string? FromProvider { get; init; }

    /// <summary>The local model that handed over.</summary>
    public string? FromModel { get; init; }

    /// <summary>Tokens sent to the local model before it handed over.</summary>
    public long? FromInputTokens { get; init; }

    /// <summary>Tokens written by the local model before it handed over.</summary>
    public long? FromOutputTokens { get; init; }

    /// <summary>From the hand-off to the first text of the target.</summary>
    public TimeSpan? FirstText { get; init; }

    /// <summary>Why the hand-off did not happen (a <c>HandOffRefusal</c> or an <c>LlmFailure</c> name); the local model then answered, or nobody after a forced one.</summary>
    public string? Failure { get; init; }
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

    /// <summary>The tool calls of the turn, in the order they were made (ADR 0017, section 5). Empty for most turns.</summary>
    public IReadOnlyList<ToolCallEntry> ToolCalls { get; init; } = [];

    /// <summary>The hand-off of the turn, when one was asked (ADR 0020). After one that answered, <see cref="Provider"/>, <see cref="Model"/> and the tokens are those of the target.</summary>
    public HandOffEntry? HandOff { get; init; }

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
