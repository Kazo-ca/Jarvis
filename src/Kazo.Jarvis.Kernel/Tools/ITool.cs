using System.Text.Json;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;

namespace Kazo.Jarvis.Kernel.Tools;

/// <summary>
/// A tool a model may ask for (ADR 0007, section 3 and ADR 0017, section 1): a stable English name, a description
/// in the language of the assistant, the JSON schema of its parameters, its class and what it does. A slice
/// registers its tools in the <see cref="IToolRegistry"/>; no call runs before the
/// <see cref="IPermissionDecision"/> allowed it.
/// </summary>
public interface ITool
{
    /// <summary>The name given to the model: English, ASCII, lower case and underscores, stable over time (<c>get_datetime</c>).</summary>
    string Name { get; }

    /// <summary>Where the tool comes from: <see cref="ToolOrigins.BuiltIn"/>, <c>mcp:&lt;server&gt;</c> or <see cref="ToolOrigins.HandOff"/>.</summary>
    string Origin { get; }

    /// <summary>
    /// Whether the tool is offered before the user chose. A tool that makes something leave the machine, or that
    /// comes from an MCP server, is not (ADR 0017, section 6).
    /// </summary>
    bool EnabledByDefault { get; }

    /// <summary>
    /// The class the tool declares: the lowest any of its calls may have. <see cref="Classify"/> can only raise it
    /// (ADR 0017, section 2).
    /// </summary>
    ActionClass DeclaredClass { get; }

    /// <summary>The JSON schema of the parameters, an object schema.</summary>
    JsonElement Schema { get; }

    /// <summary>The description given to the model.</summary>
    /// <param name="language">The language of the turn. An MCP tool keeps the description of its server.</param>
    /// <returns>The description.</returns>
    string Describe(AssistantLanguage language);

    /// <summary>
    /// The class of one call, from its arguments: the class of a file read depends on the path, that of a home
    /// automation command on the device. Throwing is a denial.
    /// </summary>
    /// <param name="arguments">The arguments of the call, a JSON object.</param>
    /// <returns>The class of this call.</returns>
    ActionClass Classify(JsonElement arguments);

    /// <summary>Runs the call. Only called after the decision point allowed it.</summary>
    /// <param name="arguments">The arguments of the call, a JSON object.</param>
    /// <param name="cancellationToken">Cancelled by an interruption or by the time limit of a call.</param>
    /// <returns>The result, returned to the model as data.</returns>
    /// <exception cref="ToolException">The call cannot be done; its message is returned to the model as the result.</exception>
    ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken);
}

/// <summary>The origins of a tool (ADR 0017, section 1).</summary>
public static class ToolOrigins
{
    /// <summary>A tool written in Jarvis.</summary>
    public const string BuiltIn = "builtin";

    /// <summary>The hand-off to a cloud target (ADR 0020).</summary>
    public const string HandOff = "hand-off";

    /// <summary>The origin of a tool of the MCP server <paramref name="server"/>.</summary>
    /// <param name="server">The name of the server in the settings.</param>
    /// <returns><c>mcp:&lt;server&gt;</c>.</returns>
    public static string Mcp(string server) => "mcp:" + server;
}

/// <summary>
/// A call that cannot be done for a reason the model can tell the user: a wrong expression, a missing parameter.
/// The message is returned to the model as data; it never holds a secret.
/// </summary>
public sealed class ToolException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ToolException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong, in one sentence.</param>
    public ToolException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong, in one sentence.</param>
    /// <param name="innerException">The cause.</param>
    public ToolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
