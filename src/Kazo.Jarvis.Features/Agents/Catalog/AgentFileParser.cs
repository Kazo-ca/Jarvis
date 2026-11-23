using Kazo.Jarvis.Kernel.Agents;

namespace Kazo.Jarvis.Features.Agents.Catalog;

/// <summary>Why the file of an agent is left out.</summary>
public enum AgentFileProblem
{
    /// <summary>The disk refused the read.</summary>
    Unreadable,

    /// <summary>The file does not start with a header between two <c>---</c> lines.</summary>
    NoHeader,

    /// <summary>The header has no <c>name</c>.</summary>
    NoName,

    /// <summary>The header has no <c>description</c>: nothing says what is sorted to this agent.</summary>
    NoDescription,

    /// <summary>Another file already gives this name to an agent.</summary>
    NameTaken,
}

/// <summary>
/// Reads the file of an agent (ADR 0025, section 2), written like an agent of Claude Code: a header between two
/// <c>---</c> lines with <c>name</c>, <c>description</c> and <c>tools</c>, then the instructions as plain text.
/// <c>tools</c> is a list separated by commas, or one <c>- name</c> per line under it.
/// </summary>
public static class AgentFileParser
{
    private const string Fence = "---";

    /// <summary>Reads one file.</summary>
    /// <param name="id">The name of the file without its extension: the identity of the agent.</param>
    /// <param name="text">The text of the file.</param>
    /// <param name="agent">The agent, when the file reads.</param>
    /// <returns>What is wrong, or <see langword="null"/>.</returns>
    public static AgentFileProblem? Parse(string id, string? text, out AgentDefinition? agent)
    {
        agent = null;
        if (text is null)
        {
            return AgentFileProblem.Unreadable;
        }

        var lines = text.ReplaceLineEndings("\n").TrimStart('﻿').Split('\n');
        var start = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        if (start < 0 || lines[start].Trim() != Fence)
        {
            return AgentFileProblem.NoHeader;
        }

        var end = Array.FindIndex(lines, start + 1, line => line.Trim() == Fence);
        if (end < 0)
        {
            return AgentFileProblem.NoHeader;
        }

        string? name = null;
        string? description = null;
        var tools = new List<string>();
        var inTools = false;
        foreach (var raw in lines[(start + 1)..end])
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (inTools && line.StartsWith('-'))
            {
                AddTools(tools, line[1..]);
                continue;
            }

            inTools = false;
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                continue;
            }

            var value = Unquote(line[(colon + 1)..]);
            switch (line[..colon].Trim().ToLowerInvariant())
            {
                case "name":
                    name = value;
                    break;
                case "description":
                    description = value;
                    break;
                case "tools":
                    inTools = true;
                    AddTools(tools, value);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return AgentFileProblem.NoName;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return AgentFileProblem.NoDescription;
        }

        var instructions = string.Join('\n', lines[(end + 1)..]).Trim();
        agent = new AgentDefinition(id, name, description, [.. tools.Distinct(StringComparer.Ordinal)], instructions);
        return null;
    }

    /// <summary>The text of a file for <paramref name="agent"/>, as <see cref="Parse"/> reads it back.</summary>
    /// <param name="agent">The agent.</param>
    /// <returns>The header, a blank line, the instructions.</returns>
    public static string Write(AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return $"{Fence}\nname: {agent.Name}\ndescription: {agent.Description}\ntools: {string.Join(", ", agent.Tools)}\n{Fence}\n\n{agent.Instructions.Trim()}\n";
    }

    private static void AddTools(List<string> tools, string value) =>
        tools.AddRange(Unquote(value).Trim('[', ']').Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(Unquote).Where(tool => tool.Length > 0));

    private static string Unquote(string value)
    {
        value = value.Trim();
        return value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')) ? value[1..^1].Trim() : value;
    }
}
