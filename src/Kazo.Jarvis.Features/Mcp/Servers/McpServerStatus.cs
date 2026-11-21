using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools.Servers;

namespace Kazo.Jarvis.Features.Mcp.Servers;

/// <summary>Where the connection to a tool server is.</summary>
public enum McpServerState
{
    /// <summary>The user turned the server off.</summary>
    Disabled,

    /// <summary>A connection attempt is under way.</summary>
    Connecting,

    /// <summary>Connected: its tools are known.</summary>
    Connected,

    /// <summary>The last attempt failed. A server reached by address is tried again later.</summary>
    Failed,

    /// <summary>A server started by command stopped; it is started again at the next call of one of its tools.</summary>
    Stopped,
}

/// <summary>What the page shows of a server.</summary>
/// <param name="Options">The server in the settings.</param>
/// <param name="State">Where its connection is.</param>
/// <param name="Failure">Why the last attempt failed, when <paramref name="State"/> is <see cref="McpServerState.Failed"/>.</param>
/// <param name="AnnouncedName">The name the server gives itself.</param>
/// <param name="Tools">Its tools, by name.</param>
public sealed record McpServerStatus(
    McpServerOptions Options,
    McpServerState State,
    ToolServerFailure? Failure,
    string? AnnouncedName,
    IReadOnlyList<McpToolStatus> Tools);

/// <summary>What the page shows of a tool of a server.</summary>
/// <param name="ServerName">Its name on the server.</param>
/// <param name="Name">Its name in the registry: the same, or prefixed by the server when the name was taken.</param>
/// <param name="Description">The whole description written by the server.</param>
/// <param name="ServerClass">The class from the declaration of the server.</param>
/// <param name="Class">The class in force, raised by the user or not.</param>
/// <param name="Enabled">Whether the tool is given to the model.</param>
/// <param name="ChangedSinceApproval">Whether the server changed the tool since the user turned it on, which turned it off.</param>
/// <param name="Tokens">A rough size of the tool in the context of the model.</param>
public sealed record McpToolStatus(
    string ServerName,
    string Name,
    string Description,
    ActionClass ServerClass,
    ActionClass Class,
    bool Enabled,
    bool ChangedSinceApproval,
    int Tokens);

/// <summary>The outcome of « Tester la connexion » (ADR 0018, section 2).</summary>
/// <param name="Failure">The named cause of the failure, or <see langword="null"/> when the server answered.</param>
/// <param name="Tools">How many tools the server offers.</param>
/// <param name="AnnouncedName">The name the server gives itself.</param>
public sealed record McpConnectionTest(ToolServerFailure? Failure, int Tools, string? AnnouncedName);
