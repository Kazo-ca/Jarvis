using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Kazo.Jarvis.Adapters.Llm;

/// <summary>
/// The tolerant parser of the local adapters (ADR 0007, section 4): a local model sometimes writes its tool call in
/// the text instead of the channel of the API. Strict rule of spike E7: the <b>whole</b> answer must be one JSON
/// object naming a declared tool; anything else is text.
/// <para>
/// For the stream: an answer that starts with <c>{</c> (or a code fence) is held back until it ends, so a tool call
/// is never said aloud. Any other answer goes on piece by piece.
/// </para>
/// </summary>
internal sealed class TolerantToolCalls
{
    private readonly IReadOnlyList<LlmTool> tools;
    private readonly bool active;
    private readonly StringBuilder held = new();

    /// <summary>Whether the start of the answer is still unknown (only white space so far).</summary>
    private bool undecided = true;

    private bool holding;

    public TolerantToolCalls(IReadOnlyList<LlmTool> tools, bool enabled)
    {
        this.tools = tools;
        active = enabled && tools.Count > 0;
    }

    /// <summary>Takes a piece of the answer and returns what can go on now: the piece, or nothing while the answer is held.</summary>
    public string Push(string piece)
    {
        if (!active || (!undecided && !holding))
        {
            return piece;
        }

        held.Append(piece);
        if (holding)
        {
            return string.Empty;
        }

        var start = held.ToString().TrimStart();
        if (start.Length == 0)
        {
            return string.Empty;
        }

        undecided = false;
        if (start[0] is '{' or '`')
        {
            holding = true;
            return string.Empty;
        }

        var text = held.ToString();
        held.Clear();
        return text;
    }

    /// <summary>
    /// At the end of the answer: the tool call the held text was, or the held text itself when it was not one.
    /// Both are <see langword="null"/> when nothing was held.
    /// </summary>
    public (FunctionCallContent? Call, string? Text) End()
    {
        if (held.Length == 0)
        {
            return (null, null);
        }

        var text = held.ToString();
        held.Clear();
        holding = false;
        return Parse(text, tools) is { } call ? (call, null) : (null, text);
    }

    /// <summary>The tool call <paramref name="content"/> is, when the whole of it is one JSON object naming a declared tool.</summary>
    internal static FunctionCallContent? Parse(string content, IReadOnlyList<LlmTool> tools)
    {
        var text = content.Trim().Trim('`');
        if (text.StartsWith("json", StringComparison.Ordinal))
        {
            text = text[4..];
        }

        try
        {
            using var document = JsonDocument.Parse(text.Trim());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("name", out var name)
                || name.ValueKind != JsonValueKind.String
                || name.GetString() is not { } toolName
                || !tools.Any(tool => tool.Name == toolName))
            {
                return null;
            }

            JsonElement? arguments = root.TryGetProperty("arguments", out var given) ? given
                : root.TryGetProperty("parameters", out var parameters) ? parameters
                : null;
            if (arguments is null)
            {
                return LlmHttp.Call(null, toolName, "{}");
            }

            return arguments.Value.ValueKind == JsonValueKind.Object ? LlmHttp.Call(null, toolName, arguments.Value.Clone()) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
