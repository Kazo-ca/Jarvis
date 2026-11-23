using System.Resources;
using Kazo.Jarvis.Kernel.Agents;
using Kazo.Jarvis.Kernel.Localization;

namespace Kazo.Jarvis.Features.Agents.Catalog;

/// <summary>
/// The agents Jarvis ships with (ADR 0025, section 3): their files are written at the first launch, in the language
/// of the assistant. The tool names are those of the tools of Jarvis; the tools of a Home Assistant server reach
/// the home automation agent by themselves, so its list is empty. The « Projet » agent comes with the projects.
/// </summary>
public static class StartingAgents
{
    private static readonly ResourceManager Texts = new(typeof(AgentTexts));

    /// <summary>Each starting agent: its identity, the key of its texts and its tools.</summary>
    private static readonly (string Id, string Key, string[] Tools)[] All =
    [
        (AgentIds.Main, "Jarvis", ["get_datetime", "calculate", "hand_off_to_claude", "start_quiet_period"]),
        (AgentIds.HomeAutomation, "Home", []),
        ("web", "Web", ["web_search", "fetch_url"]),
        ("files", "Files", ["list_directory", "read_file"]),
        ("claude-code", "ClaudeCode", ["get_claude_sessions"]),
        ("memory", "Memory", ["remember_fact", "forget_fact", "search_memory"]),
        (AgentIds.Project, "Project", ["list_directory", "read_file", "get_claude_sessions"]),
    ];

    /// <summary>The starting agents, written in <paramref name="language"/>.</summary>
    /// <param name="language">The language of the assistant.</param>
    /// <returns>The agents, the Jarvis agent first.</returns>
    public static IReadOnlyList<AgentDefinition> In(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return [.. All.Select(agent => new AgentDefinition(
            agent.Id,
            Text(agent.Key + "_Name", language),
            Text(agent.Key + "_Description", language),
            agent.Tools,
            Text(agent.Key + "_Instructions", language)))];
    }

    /// <summary>The Jarvis agent Jarvis ships with: it answers when its file is missing or unreadable.</summary>
    /// <param name="language">The language of the assistant.</param>
    /// <returns>The agent.</returns>
    public static AgentDefinition Main(AssistantLanguage language) => In(language)[0];

    /// <summary>A text of the agents, in <paramref name="language"/>.</summary>
    /// <param name="key">The key in <c>AgentTexts.resx</c>.</param>
    /// <param name="language">The language.</param>
    /// <param name="arguments">The values of its placeholders.</param>
    /// <returns>The text.</returns>
    public static string Text(string key, AssistantLanguage language, params object[] arguments)
    {
        ArgumentNullException.ThrowIfNull(language);
        var text = Texts.GetString(key, language.Culture) ?? key;
        return arguments.Length == 0 ? text : string.Format(System.Globalization.CultureInfo.InvariantCulture, text, arguments);
    }
}

/// <summary>Marker of the resources <c>AgentTexts.resx</c> and <c>AgentTexts.fr.resx</c>.</summary>
public sealed class AgentTexts
{
    private AgentTexts()
    {
    }
}
