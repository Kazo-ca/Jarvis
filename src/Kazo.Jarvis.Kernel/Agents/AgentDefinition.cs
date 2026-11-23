namespace Kazo.Jarvis.Kernel.Agents;

/// <summary>
/// An agent (ADR 0025, section 1): a name, one sentence that says what it is for, its instructions and the tools a
/// model sees while it answers. It gives no right: the class of every call and its confirmation stay those of the
/// decision point (ADR 0017).
/// </summary>
/// <param name="Id">Its identity: the name of its file without the extension (<c>home-automation</c>). Stable, lower case.</param>
/// <param name="Name">The name shown, said and written by a model that hands over (« Domotique »).</param>
/// <param name="Description">One sentence: what is sorted to this agent.</param>
/// <param name="Tools">The names of the tools its file lists. A tool that is off stays invisible.</param>
/// <param name="Instructions">Its instructions, added to the system message while it answers.</param>
public sealed record AgentDefinition(string Id, string Name, string Description, IReadOnlyList<string> Tools, string Instructions);

/// <summary>The identities the code knows (ADR 0025, sections 3 and 9).</summary>
public static class AgentIds
{
    /// <summary>The Jarvis agent: it receives the request, answers the simple ones and hands the others over.</summary>
    public const string Main = "jarvis";

    /// <summary>The agent that gets the tools of the Home Assistant servers.</summary>
    public const string HomeAutomation = "home-automation";

    /// <summary>The one agent that works on a project: it is loaded with the folder and the memory of the project asked (section 13, point 2).</summary>
    public const string Project = "project";
}
