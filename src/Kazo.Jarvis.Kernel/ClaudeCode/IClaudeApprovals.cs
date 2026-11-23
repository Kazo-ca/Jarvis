using System.Text.Json;

namespace Kazo.Jarvis.Kernel.ClaudeCode;

/// <summary>
/// The single place where a write or a command of a Claude Code session started by Jarvis is decided (ADR 0021,
/// section 4). The local server receives the request by its <c>approve</c> tool and hands it over here; the slice
/// that started the session classifies it and asks the decision point of ADR 0017.
/// </summary>
public interface IClaudeApprovals
{
    /// <summary>Decides one request of a session.</summary>
    /// <param name="request">The request, as Claude Code sent it.</param>
    /// <param name="cancellationToken">Cancelled when Claude Code gives the call up.</param>
    /// <returns>Allowed, or denied with the cause to tell the session.</returns>
    ValueTask<ClaudeApproval> AskAsync(ClaudeApprovalRequest request, CancellationToken cancellationToken = default);
}

/// <summary>A permission request of a Claude Code session.</summary>
/// <param name="TaskId">The task the session named, or <see langword="null"/>: such a request is denied.</param>
/// <param name="ToolName">The tool of Claude Code: <c>Write</c>, <c>Bash</c>…</param>
/// <param name="Input">The input of the tool. Data: it is shown and classified, never obeyed.</param>
/// <param name="ToolUseId">The identifier Claude Code gave to this use of the tool.</param>
public sealed record ClaudeApprovalRequest(string? TaskId, string ToolName, JsonElement Input, string? ToolUseId = null);

/// <summary>The answer to a permission request. An allowed request goes back with its input unchanged: Jarvis never corrects an input.</summary>
/// <param name="Allowed">Whether the session may use the tool.</param>
/// <param name="Message">The cause of a denial, told to the session.</param>
public sealed record ClaudeApproval(bool Allowed, string? Message = null)
{
    /// <summary>Allowed.</summary>
    public static ClaudeApproval Allow { get; } = new(true);

    /// <summary>Denied, with the cause.</summary>
    /// <param name="message">The cause, for the session.</param>
    /// <returns>The denial.</returns>
    public static ClaudeApproval Deny(string message) => new(false, message);
}
