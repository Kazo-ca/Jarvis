using System.Text.Json.Serialization;
using Kazo.Jarvis.Kernel.Permissions;

namespace Kazo.Jarvis.Features.Permissions.Decision;

/// <summary>
/// One decision, as the log keeps it in the collection <c>Permissions.Decisions</c> of <c>jarvis.db</c>
/// (ADR 0017, section 2). « Tout effacer » erases it with the rest of the store.
/// </summary>
public sealed record DecisionRecord
{
    /// <summary>When the action was asked for.</summary>
    public DateTimeOffset At { get; init; }

    /// <summary>A tool call, a session or a task.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RequesterKind>))]
    public RequesterKind RequesterKind { get; init; }

    /// <summary>The name of who asked.</summary>
    public string Requester { get; init; } = string.Empty;

    /// <summary>The name of the tool, as the model wrote it.</summary>
    public string Tool { get; init; } = string.Empty;

    /// <summary>The arguments as JSON, cut at <see cref="PermissionDecisionPoint.MostArgumentCharacters"/> characters.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>The class of the call, when it could be told.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ActionClass>))]
    public ActionClass? Class { get; init; }

    /// <summary>Whether the action was allowed.</summary>
    public bool Allowed { get; init; }

    /// <summary>For a denial, why.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<DenialCause>))]
    public DenialCause? Cause { get; init; }

    /// <summary>The channel of the human answer, when there was one (ADR 0023, section 5): <c>voice</c> or <c>click</c>.</summary>
    public string? Channel { get; init; }
}
