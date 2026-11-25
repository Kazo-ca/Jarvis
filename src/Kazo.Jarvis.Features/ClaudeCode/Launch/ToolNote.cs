using System.Text.Json;

namespace Kazo.Jarvis.Features.ClaudeCode.Launch;

/// <summary>
/// What the log of a task keeps of a tool a session used (ADR 0026, section 5): the tool and what it was used
/// on, never the content it wrote. What a session writes is data: it is read, cut, and never obeyed.
/// </summary>
internal static class ToolNote
{
    // Where the tools of Claude Code say what they work on, the most telling first.
    private static readonly string[] Fields = ["file_path", "notebook_path", "path", "command", "pattern", "url", "query", "description"];
    private static readonly string[] PathFields = ["file_path", "notebook_path"];

    /// <summary>Reads one use of a tool.</summary>
    /// <param name="tool">The tool.</param>
    /// <param name="input">Its input.</param>
    /// <returns>The line of the log, and the file the tool writes when it is a write tool that names one.</returns>
    public static (string About, string? Path) Of(string tool, JsonElement input)
    {
        var on = Fields.Select(field => Text(input, field)).FirstOrDefault(text => text is not null);
        var path = SessionRules.WritesOrRuns(tool) && tool != SessionRules.Bash
            ? PathFields.Select(field => Text(input, field)).FirstOrDefault(text => text is not null)
            : null;
        return (on is null ? tool : tool + " " + on, path);
    }

    private static string? Text(JsonElement input, string field) =>
        input.ValueKind == JsonValueKind.Object && input.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text
            ? text
            : null;
}
