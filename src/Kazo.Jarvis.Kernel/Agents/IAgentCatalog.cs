using System.Text.Json;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Kernel.Agents;

/// <summary>
/// The agents Jarvis knows (ADR 0025). The agents slice reads their files and keeps the two tools by which a turn
/// changes hands; the conversation slice asks it who answers and with which tools, and runs the turn itself.
/// Without an implementation, a turn gives every enabled tool to the model, as before the agents existed.
/// </summary>
public interface IAgentCatalog
{
    /// <summary>Raised after the agents were read again; on any thread.</summary>
    event Action? Changed;

    /// <summary>The agents, the Jarvis agent first, then by name.</summary>
    IReadOnlyList<AgentDefinition> Agents { get; }

    /// <summary>The Jarvis agent (ADR 0025, section 9). Always there: when its file is missing or unreadable, the one Jarvis ships with answers.</summary>
    AgentDefinition Main { get; }

    /// <summary>The name of the tool by which the Jarvis agent hands the turn to another agent.</summary>
    string HandToolName { get; }

    /// <summary>The name of the tool by which another agent says « this is not for me » (ADR 0025, section 8).</summary>
    string ReturnToolName { get; }

    /// <summary>The agent a model or the user named.</summary>
    /// <param name="name">Its name or its identity; case and accents do not count.</param>
    /// <returns>The agent, or <see langword="null"/>.</returns>
    AgentDefinition? Find(string? name);

    /// <summary>
    /// The enabled tools a model sees while <paramref name="agent"/> answers (ADR 0025, sections 1 and 3): those its
    /// file names and those whose source gave them to it (<see cref="ITool.Agent"/>). The Jarvis agent also gets
    /// every tool no agent claims, so a new tool is never lost, and the tool that hands over when another agent
    /// exists; the others get the tool that hands back.
    /// </summary>
    /// <param name="agent">An agent of the catalog.</param>
    /// <returns>The tools, by name.</returns>
    IReadOnlyList<ITool> ToolsOf(AgentDefinition agent);

    /// <summary>The arguments of a call of <see cref="HandToolName"/>.</summary>
    /// <param name="arguments">The arguments, a JSON object.</param>
    /// <returns>The agent named and the reason; <see cref="AgentHand.Agent"/> is <see langword="null"/> for a name no agent has, or an agent without any tool now.</returns>
    AgentHand ReadHand(JsonElement arguments);
}

/// <summary>What a model wrote when it handed the turn to an agent.</summary>
/// <param name="Agent">The agent, or <see langword="null"/> when the name is unknown: the hand is then refused.</param>
/// <param name="Asked">The name as the model wrote it.</param>
public sealed record AgentHand(AgentDefinition? Agent, string? Asked);
