using System.Text;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Features.Agents.Catalog;

/// <summary>
/// The tool <c>hand_to_agent</c> (ADR 0025, section 4), by which the Jarvis agent hands the turn to another agent.
/// Its description holds one line per agent, name and description, and nothing of their tools: the request of the
/// Jarvis agent stays short. Class « Lecture »: an agent gives no right, each call of the next agent goes through
/// the decision point.
/// <para>
/// The conversation recognizes it by its name and continues the turn with the agent named.
/// <see cref="InvokeAsync"/> is therefore never the way it runs.
/// </para>
/// </summary>
public sealed class HandToAgentTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "hand_to_agent";

    private readonly Func<AgentCatalog> catalog;

    /// <summary>Creates the tool.</summary>
    /// <param name="catalog">The catalog, asked late: it reads the registry this tool is in.</param>
    public HandToAgentTool(Func<AgentCatalog> catalog)
    {
        this.catalog = catalog;
    }

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.Read;

    /// <summary>The names of the agents that can take the hand now are the only values of <c>agent</c>.</summary>
    public JsonElement Schema => JsonSerializer.SerializeToElement(new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["agent"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = catalog().Others.Select(agent => agent.Name).ToArray() },
            ["reason"] = new { type = "string", description = "One short sentence: why this agent." },
        },
        ["required"] = new[] { "agent" },
    });

    /// <inheritdoc />
    public string Describe(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        var text = new StringBuilder(StartingAgents.Text("Hand_Description", language));
        foreach (var agent in catalog().Others)
        {
            text.Append('\n').Append("- ").Append(agent.Name).Append(": ").Append(agent.Description);
        }

        return text.ToString();
    }

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) => ActionClass.Read;

    /// <inheritdoc />
    public ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        throw new ToolException("The hand only changes inside a turn of the conversation.");
}

/// <summary>
/// The tool <c>return_to_jarvis</c> (ADR 0025, section 8): an agent that received a request that is not for it says
/// so, and the Jarvis agent takes the turn back. Offered to every agent but the Jarvis agent.
/// </summary>
public sealed class ReturnToJarvisTool : ITool
{
    /// <summary>The name given to the model.</summary>
    public const string ToolName = "return_to_jarvis";

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Origin => ToolOrigins.BuiltIn;

    /// <inheritdoc />
    public bool EnabledByDefault => true;

    /// <inheritdoc />
    public ActionClass DeclaredClass => ActionClass.Read;

    /// <inheritdoc />
    public JsonElement Schema { get; } = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new { reason = new { type = "string", description = "One short sentence: why this request is not for you." } },
    });

    /// <inheritdoc />
    public string Describe(AssistantLanguage language) => StartingAgents.Text("Return_Description", language);

    /// <inheritdoc />
    public ActionClass Classify(JsonElement arguments) => ActionClass.Read;

    /// <inheritdoc />
    public ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        throw new ToolException("The hand only changes inside a turn of the conversation.");
}
