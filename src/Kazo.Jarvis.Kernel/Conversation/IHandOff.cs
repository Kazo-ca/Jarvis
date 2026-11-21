using System.Text.Json;
using Kazo.Jarvis.Kernel.Llm;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Kernel.Conversation;

/// <summary>
/// The hand-off of a turn to a cloud target (ADR 0020). The hand-off slice keeps the targets, the tool and the
/// formulas that force it; the conversation slice asks it who answers, then runs the answer itself, in the same
/// loop and through the same decision point as the local model. Without an implementation, nothing is handed off.
/// </summary>
public interface IHandOff
{
    /// <summary>The name of the tool by which the local model hands off (<c>hand_off_to_claude</c>).</summary>
    string ToolName { get; }

    /// <summary>
    /// Whether <paramref name="request"/> starts with a formula that forces the hand-off (« Demande à Claude… »,
    /// ADR 0020, section 3). Recognized by code, never by a model.
    /// </summary>
    /// <param name="request">The request, as heard or typed.</param>
    /// <returns>The kind and what follows the formula, or <see langword="null"/>.</returns>
    ForcedHandOff? Forced(string request);

    /// <summary>The arguments of a call of the tool.</summary>
    /// <param name="arguments">The arguments, a JSON object.</param>
    /// <returns>The kind and the reason, or <see langword="null"/> when they cannot be read.</returns>
    HandOffCall? Read(JsonElement arguments);

    /// <summary>Who answers a hand-off of <paramref name="kind"/> now.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The target, or why there is none.</returns>
    HandOffOpening Open(HandOffKind kind);
}

/// <summary>The two uses of the hand-off (ADR 0020, section 1).</summary>
public enum HandOffKind
{
    /// <summary>A question the local model cannot treat; answered by a cloud provider.</summary>
    Question,

    /// <summary>A work on files or in several steps; done by Claude Code (ADR 0021).</summary>
    Task,
}

/// <summary>Why a hand-off cannot start. Each cause has its own sentence.</summary>
public enum HandOffRefusal
{
    /// <summary>No target is chosen for the questions, or the provider chosen is gone.</summary>
    NoQuestionsTarget,

    /// <summary>No target is chosen for the tasks.</summary>
    NoTasksTarget,
}

/// <summary>A hand-off forced by the first words of a request.</summary>
/// <param name="Kind">The target the formula names.</param>
/// <param name="Request">What follows the formula: the request sent to the target.</param>
public sealed record ForcedHandOff(HandOffKind Kind, string Request);

/// <summary>What the local model wrote when it handed off.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Reason">One short sentence told to the user, or <see langword="null"/>.</param>
public sealed record HandOffCall(HandOffKind Kind, string? Reason);

/// <summary>The cloud provider that answers a question handed off.</summary>
/// <param name="Provider">The provider.</param>
/// <param name="Client">Its client, kept by the hand-off slice: the caller never disposes it.</param>
/// <param name="Name">The name said to the user (« Claude », « ChatGPT »).</param>
/// <param name="WithMemory">Whether the kept facts and the summary go with the question (ADR 0020, section 4). Off by default.</param>
public sealed record HandOffTarget(LlmProvider Provider, IChatClient Client, string Name, bool WithMemory);

/// <summary>The answer of <see cref="IHandOff.Open"/>: a target, or the cause of its absence.</summary>
/// <param name="Target">The target, when the hand-off can start.</param>
/// <param name="Refusal">Why it cannot, when <paramref name="Target"/> is <see langword="null"/>.</param>
public sealed record HandOffOpening(HandOffTarget? Target, HandOffRefusal? Refusal)
{
    /// <summary>The hand-off can start.</summary>
    /// <param name="target">Who answers.</param>
    /// <returns>The opening.</returns>
    public static HandOffOpening To(HandOffTarget target) => new(target, null);

    /// <summary>The hand-off cannot start.</summary>
    /// <param name="refusal">Why.</param>
    /// <returns>The opening.</returns>
    public static HandOffOpening Refused(HandOffRefusal refusal) => new(null, refusal);
}
