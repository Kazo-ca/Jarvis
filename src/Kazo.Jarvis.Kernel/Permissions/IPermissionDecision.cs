using System.Text.Json;

namespace Kazo.Jarvis.Kernel.Permissions;

/// <summary>
/// The single decision point of the permissions (ADR 0017, section 2): "is this action allowed?". Every action a
/// model asks for goes through it, whoever asks: a tool call of the local model, a tool call of a cloud target, a
/// permission request of a Claude Code session. The model proposes, this code decides (ADR 0009, principle 1).
/// </summary>
public interface IPermissionDecision
{
    /// <summary>Decides one action. Never throws for a denial: a failure of any step is a denial (ADR 0009, principle 3).</summary>
    /// <param name="request">Who asks, and what exactly.</param>
    /// <param name="cancellationToken">An interruption by the user; it is a denial for the log.</param>
    /// <returns>Allowed, or denied with the cause.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; the denial is logged first.</exception>
    ValueTask<PermissionDecision> DecideAsync(ActionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Who asks for an action.</summary>
public enum RequesterKind
{
    /// <summary>A tool call made by a model of the conversation.</summary>
    Tool,

    /// <summary>A Claude Code session launched by Jarvis.</summary>
    Session,

    /// <summary>An autonomous task.</summary>
    Task,
}

/// <summary>Who asks for an action (ADR 0017, section 2).</summary>
/// <param name="Kind">A tool call, a session or a task.</param>
/// <param name="Name">Its name for the log: the provider of the model, the session, the task. Never a secret.</param>
public sealed record ActionRequester(RequesterKind Kind, string Name);

/// <summary>One action asked for: the exact tool name and arguments.</summary>
/// <param name="Requester">Who asks.</param>
/// <param name="Tool">The name of the tool, as the model wrote it.</param>
/// <param name="Arguments">The arguments, a JSON object; <see langword="null"/> when they could not be read, which is a denial.</param>
public sealed record ActionRequest(ActionRequester Requester, string Tool, JsonElement? Arguments)
{
    /// <summary>The turn the action was asked in; <see cref="ActionContext.None"/> outside a turn.</summary>
    public ActionContext Context { get; init; } = ActionContext.None;
}

/// <summary>Why an action was denied. The slice that asked turns it into one sentence for the model.</summary>
public enum DenialCause
{
    /// <summary>No tool of the registry has this name.</summary>
    UnknownTool,

    /// <summary>The tool exists but the user has not enabled it.</summary>
    ToolDisabled,

    /// <summary>The arguments are not a JSON object.</summary>
    UnreadableArguments,

    /// <summary>The classifier of the tool failed: without a class, nothing is allowed.</summary>
    ClassifierFailed,

    /// <summary>The call is of class <see cref="ActionClass.Forbidden"/>.</summary>
    Forbidden,

    /// <summary>The call needs a confirmation and this host has nothing to ask it with.</summary>
    ConfirmationUnavailable,

    /// <summary>The user refused.</summary>
    Refused,

    /// <summary>Nobody answered in time.</summary>
    TimedOut,

    /// <summary>The user interrupted the turn.</summary>
    Interrupted,
}

/// <summary>The answer of the decision point.</summary>
/// <param name="Allowed">Whether the action may run.</param>
/// <param name="Class">The class of this call, when it could be told.</param>
/// <param name="Cause">For a denial, why.</param>
public sealed record PermissionDecision(bool Allowed, ActionClass? Class, DenialCause? Cause)
{
    /// <summary>The channel of the human answer (<see cref="ConfirmationChannels"/>), when there was one.</summary>
    public string? Channel { get; init; }

    /// <summary>An allowed action of class <paramref name="actionClass"/>.</summary>
    /// <param name="actionClass">The class of the call.</param>
    /// <returns>The decision.</returns>
    public static PermissionDecision Allow(ActionClass actionClass) => new(true, actionClass, null);

    /// <summary>A denial.</summary>
    /// <param name="cause">Why.</param>
    /// <param name="actionClass">The class of the call, when it could be told.</param>
    /// <returns>The decision.</returns>
    public static PermissionDecision Deny(DenialCause cause, ActionClass? actionClass = null) => new(false, actionClass, cause);
}
