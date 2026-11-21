using System.Globalization;
using System.Text.Json;
using Kazo.Jarvis.Kernel.ClaudeCode;

namespace Kazo.Jarvis.Adapters.ClaudeCode;

/// <summary>
/// Reads the lines Claude Code writes with <c>--output-format stream-json</c> (spikes E8 and E12). The format
/// belongs to Claude Code and may change: a line that is not understood gives nothing, it never fails.
/// </summary>
internal static class ClaudeStream
{
    /// <summary>The events one line holds.</summary>
    /// <param name="line">One line of the output.</param>
    /// <returns>Zero, one or several events.</returns>
    public static IReadOnlyList<ClaudeRunEvent> Read(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return Text(root, "type") switch
            {
                "system" when Text(root, "subtype") == "init" && Text(root, "session_id") is { } session =>
                    [new ClaudeStarted(session, Text(root, "model"))],
                "assistant" => [.. Blocks(root, "tool_use").Select(ToolUse).OfType<ClaudeRunEvent>()],
                "user" => [.. Blocks(root, "tool_result").Select(ToolResult).OfType<ClaudeRunEvent>()],
                "result" => [Result(root)],
                _ => [],
            };
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>The line that gives a session its message, for <c>--input-format stream-json</c>.</summary>
    /// <param name="message">The message.</param>
    /// <returns>One line of JSON.</returns>
    public static string UserMessage(string message) => JsonSerializer.Serialize(new
    {
        type = "user",
        message = new { role = "user", content = message },
    });

    private static IEnumerable<JsonElement> Blocks(JsonElement root, string type)
    {
        if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind == JsonValueKind.Object && Text(block, "type") == type)
                {
                    yield return block;
                }
            }
        }
    }

    private static ClaudeToolUse? ToolUse(JsonElement block) =>
        Text(block, "id") is { } id && Text(block, "name") is { } name
            ? new ClaudeToolUse(id, name, block.TryGetProperty("input", out var input) ? input.Clone() : default)
            : null;

    private static ClaudeToolResult? ToolResult(JsonElement block) =>
        Text(block, "tool_use_id") is { } id
            ? new ClaudeToolResult(id, block.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True)
            : null;

    private static ClaudeTurnEnded Result(JsonElement root)
    {
        decimal? cost = null;
        if (root.TryGetProperty("total_cost_usd", out var announced) && announced.ValueKind == JsonValueKind.Number
            && decimal.TryParse(announced.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
        {
            cost = parsed;
        }

        var denials = root.TryGetProperty("permission_denials", out var denied) && denied.ValueKind == JsonValueKind.Array ? denied.GetArrayLength() : 0;
        var failed = (root.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True)
            || (Text(root, "subtype") is { } subtype && subtype != "success");
        return new ClaudeTurnEnded(Text(root, "result"), cost, failed, denials);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
