using System.Text.Json;
using Kazo.Jarvis.Kernel.Permissions;

namespace Kazo.Jarvis.Kernel.Tools.Servers;

/// <summary>
/// Classes the calls of the tools of one kind of tool server (ADR 0018, sections 5 and 7). The slice that knows a
/// kind of server registers one; the slice of the servers asks it for every call of a server the user gave that
/// kind. Without one, a server is classed by the general rule: a declared read is a read, the rest is sensitive.
/// </summary>
public interface IToolServerClassifier
{
    /// <summary>The kind of server it classes, for instance <see cref="ToolServerKinds.HomeAssistant"/>.</summary>
    string Kind { get; }

    /// <summary>The lowest class any call of <paramref name="tool"/> may have: what the tools page shows.</summary>
    /// <param name="tool">The tool, as its server describes it.</param>
    /// <returns>The class.</returns>
    ActionClass LowestClass(ToolServerTool tool);

    /// <summary>The class of one call. Throwing is a denial.</summary>
    /// <param name="call">The call, and a way to ask the same server a question first.</param>
    /// <param name="cancellationToken">Cancels the classification.</param>
    /// <returns>The class of this call.</returns>
    ValueTask<ActionClass> ClassifyAsync(ToolServerCall call, CancellationToken cancellationToken = default);

    /// <summary>
    /// The arguments to send for <paramref name="call"/>, once what the model wrote is matched to what the server
    /// knows: the model writes « lumière articulée » where the device is named « Articulé ». The class of the call
    /// is asked on the arguments this returns, and they are the ones sent. Unchanged by default.
    /// </summary>
    /// <param name="call">The call the model asked for.</param>
    /// <param name="cancellationToken">Cancels the question to the server.</param>
    /// <returns>The arguments to class and to send.</returns>
    ValueTask<JsonElement> ResolveAsync(ToolServerCall call, CancellationToken cancellationToken = default) => ValueTask.FromResult(call.Arguments);

    /// <summary>
    /// What to add to the failure the server gave for <paramref name="call"/>, so that the model can try again: for
    /// instance the names the server knows. Nothing by default.
    /// </summary>
    /// <param name="call">The call that failed.</param>
    /// <param name="cancellationToken">Cancels the question to the server.</param>
    /// <returns>A text for the model, in English, or <see langword="null"/>.</returns>
    ValueTask<string?> HintAsync(ToolServerCall call, CancellationToken cancellationToken = default) => ValueTask.FromResult<string?>(null);
}

/// <summary>A call to class.</summary>
/// <param name="Server">The name of the server in the settings.</param>
/// <param name="Tool">The tool, as its server describes it.</param>
/// <param name="Arguments">The arguments of the call, a JSON object.</param>
/// <param name="Ask">
/// Calls another tool of the same server, by its name on the server, whatever its switch: the classifier is code of
/// Jarvis that reads, not the model.
/// </param>
public sealed record ToolServerCall(
    string Server,
    ToolServerTool Tool,
    JsonElement Arguments,
    Func<string, JsonElement, CancellationToken, ValueTask<ToolServerResult>> Ask);

/// <summary>The kinds of tool servers that have their own classification.</summary>
public static class ToolServerKinds
{
    /// <summary>The MCP server of Home Assistant (ADR 0018, section 5).</summary>
    public const string HomeAssistant = "home-assistant";
}
