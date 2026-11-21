using Kazo.Jarvis.Kernel.Assistant;

namespace Kazo.Jarvis.Kernel.Permissions;

/// <summary>
/// Asks the user to approve one action (ADR 0009, principle 2; ADR 0023). The decision point calls it for the
/// "Sensitive" and "Critical" classes; the slice <c>Permissions/Confirmation</c> answers. Only a human answer
/// approves: no model and no text coming from a model goes through here.
/// </summary>
public interface IConfirmation
{
    /// <summary>
    /// Opens a request and waits for its answer or an interruption, as long as it takes: Jarvis never decides in
    /// place of the user, so no timeout refuses for them (ADR 0023, section 4). Never throws for a refusal.
    /// </summary>
    /// <param name="request">What is asked, and by whom.</param>
    /// <param name="cancellationToken">An interruption of the turn: the request closes with <see cref="ConfirmationAnswer.Interrupted"/>.</param>
    /// <returns>The answer and its channel.</returns>
    ValueTask<ConfirmationOutcome> AskAsync(ConfirmationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>One action to approve.</summary>
/// <param name="Requester">Who asks.</param>
/// <param name="Tool">The exact name of the tool.</param>
/// <param name="Summary">One sentence in the language of the assistant, written by the tool from its arguments, never by a model; <see langword="null"/> for the general sentence that names the tool.</param>
/// <param name="Detail">The exact arguments, nothing cut.</param>
/// <param name="Class">"Sensitive" or "Critical".</param>
/// <param name="Context">The turn the action was asked in.</param>
public sealed record ConfirmationRequest(
    ActionRequester Requester,
    string Tool,
    string? Summary,
    string Detail,
    ActionClass Class,
    ActionContext Context);

/// <summary>How a request ended.</summary>
public enum ConfirmationAnswer
{
    /// <summary>The user approved, through a channel the class accepts.</summary>
    Approved,

    /// <summary>The user refused, or answered something that is not an agreement.</summary>
    Refused,

    /// <summary>The turn was interrupted.</summary>
    Interrupted,
}

/// <summary>The answer to a request.</summary>
/// <param name="Answer">How it ended.</param>
/// <param name="Channel">The channel of the human answer (<see cref="ConfirmationChannels"/>), or <see langword="null"/> when there was none.</param>
public sealed record ConfirmationOutcome(ConfirmationAnswer Answer, string? Channel);

/// <summary>The channels of a human answer, as the log keeps them (ADR 0023, section 5).</summary>
public static class ConfirmationChannels
{
    /// <summary>A spoken answer, recognized by code.</summary>
    public const string Voice = "voice";

    /// <summary>A click in the confirmation window.</summary>
    public const string Click = "click";
}

/// <summary>The turn an action was asked in: it decides what Jarvis says aloud (ADR 0023, section 2).</summary>
/// <param name="Turn">The turn, or <see cref="TurnId.None"/> outside a turn.</param>
/// <param name="Spoken">Whether the user spoke the request of this turn.</param>
public sealed record ActionContext(TurnId Turn, bool Spoken)
{
    /// <summary>An action asked outside a turn.</summary>
    public static ActionContext None { get; } = new(TurnId.None, false);
}
