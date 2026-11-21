using System.Security.Cryptography;
using System.Text;
using Kazo.Jarvis.Kernel.Tools.Servers;

namespace Kazo.Jarvis.Features.Mcp.Servers;

/// <summary>
/// The fingerprint of what a tool says it does (ADR 0018, section 3): its name, its description and its schema. The
/// user approves a tool with one fingerprint; a server cannot change in silence what an approved tool says it does.
/// </summary>
public static class McpToolFingerprint
{
    /// <summary>The fingerprint of a tool.</summary>
    /// <param name="tool">The tool, as its server describes it.</param>
    /// <returns>The SHA-256 of its three parts, in hexadecimal.</returns>
    public static string Of(ToolServerTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        // The length before each part: « ab » + « c » and « a » + « bc » must not give the same fingerprint.
        var text = new StringBuilder();
        foreach (var part in new[] { tool.Name, tool.Description, tool.Schema.ValueKind == System.Text.Json.JsonValueKind.Undefined ? string.Empty : tool.Schema.GetRawText(), tool.ReadOnly ? "read-only" : "writes" })
        {
            text.Append(part.Length).Append(':').Append(part).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
