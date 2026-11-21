using System.Text.Json;

namespace Kazo.Jarvis.Adapters.Llm;

/// <summary>A message as the three adapters read it before writing it in the format of their provider.</summary>
/// <param name="Role"><c>system</c>, <c>user</c>, <c>assistant</c> or <c>tool</c>.</param>
/// <param name="Text">The text; for the role <c>tool</c>, the result of the call.</param>
internal sealed record LlmMessage(string Role, string Text)
{
    /// <summary>For an assistant message, the tool calls it made.</summary>
    public IReadOnlyList<LlmToolCall> Calls { get; init; } = [];

    /// <summary>For the role <c>tool</c>, the call this result answers.</summary>
    public string? CallId { get; init; }

    /// <summary>For the role <c>tool</c>, the name of the tool, when the call is in the messages.</summary>
    public string? ToolName { get; init; }
}

/// <summary>A tool call made by the model in an earlier round of the turn.</summary>
/// <param name="Id">The identifier that ties the result to the call.</param>
/// <param name="Name">The name of the tool.</param>
/// <param name="Arguments">The arguments, a JSON object.</param>
internal sealed record LlmToolCall(string Id, string Name, JsonElement Arguments);

/// <summary>A tool declared to the model (ADR 0007, section 3).</summary>
/// <param name="Name">Its name.</param>
/// <param name="Description">Its description, in the language of the assistant.</param>
/// <param name="Schema">The JSON schema of its parameters.</param>
internal sealed record LlmTool(string Name, string Description, JsonElement Schema);
