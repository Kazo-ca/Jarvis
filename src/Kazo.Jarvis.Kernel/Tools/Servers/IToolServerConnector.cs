using System.Text.Json;

namespace Kazo.Jarvis.Kernel.Tools.Servers;

/// <summary>
/// Opens a connection to a tool server (ADR 0018, section 1): a program or a service that offers tools over the
/// Model Context Protocol. The kernel names no type of the package that speaks the protocol; the adapter does.
/// </summary>
public interface IToolServerConnector
{
    /// <summary>Connects to a server and does the opening exchange of the protocol.</summary>
    /// <param name="endpoint">Where the server is, or which command starts it.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>The open connection. Disposing it closes the session and stops a server started by command.</returns>
    /// <exception cref="ToolServerException">The connection failed; the failure names the cause.</exception>
    ValueTask<IToolServerConnection> ConnectAsync(ToolServerEndpoint endpoint, CancellationToken cancellationToken = default);
}

/// <summary>An open connection to a tool server.</summary>
public interface IToolServerConnection : IAsyncDisposable
{
    /// <summary>The name the server gives itself. Shown to the user, never trusted (ADR 0018, section 1).</summary>
    string? AnnouncedName { get; }

    /// <summary>
    /// Completes when the connection ends by itself: the process of a server started by command stopped, or the
    /// stream closed. A server reached by address that keeps no session may never complete it.
    /// </summary>
    Task Closed { get; }

    /// <summary>The tools the server offers now.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The tools, as the server describes them.</returns>
    /// <exception cref="ToolServerException">The server did not answer.</exception>
    ValueTask<IReadOnlyList<ToolServerTool>> ListToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls a tool.</summary>
    /// <param name="tool">The name of the tool on the server.</param>
    /// <param name="arguments">The arguments, a JSON object.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What the tool answered, as text; <see cref="ToolServerResult.IsError"/> when the tool itself reports a failure.</returns>
    /// <exception cref="ToolServerException">The call did not reach the server or got no answer.</exception>
    ValueTask<ToolServerResult> CallAsync(string tool, JsonElement arguments, CancellationToken cancellationToken = default);
}

/// <summary>
/// Where a tool server is. Either <see cref="Address"/> or <see cref="Command"/> is set. A class and not a record:
/// its headers hold secrets, which must never be printed by a generated <c>ToString</c>.
/// </summary>
public sealed class ToolServerEndpoint
{
    /// <summary>The name of the server in the settings, for the logs.</summary>
    public required string Name { get; init; }

    /// <summary>The address of a server reached over HTTP.</summary>
    public Uri? Address { get; init; }

    /// <summary>The command that starts a local server which speaks on its standard input and output.</summary>
    public string? Command { get; init; }

    /// <summary>The arguments of <see cref="Command"/>.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>The headers sent with every request to <see cref="Address"/>, with their values read from the vault.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>A tool as its server describes it.</summary>
/// <param name="Name">The name on the server.</param>
/// <param name="Description">The description written by the server: a text that enters the context of the model.</param>
/// <param name="Schema">The JSON schema of the parameters.</param>
/// <param name="ReadOnly">Whether the server declares that the tool changes nothing. A declaration, not a proof.</param>
public sealed record ToolServerTool(string Name, string Description, JsonElement Schema, bool ReadOnly);

/// <summary>The answer of a tool.</summary>
/// <param name="Text">The text parts of the answer, joined.</param>
/// <param name="IsError">Whether the tool reports that it failed.</param>
public sealed record ToolServerResult(string Text, bool IsError);

/// <summary>Why a tool server cannot be used (ADR 0018, section 2).</summary>
public enum ToolServerFailure
{
    /// <summary>The server cannot be reached.</summary>
    Unreachable,

    /// <summary>The server refused the token or the headers.</summary>
    Rejected,

    /// <summary>The server took too long.</summary>
    TimedOut,

    /// <summary>The command that starts the server was not found or could not start.</summary>
    CommandNotFound,

    /// <summary>What answered does not speak the protocol.</summary>
    Protocol,

    /// <summary>The connection ended: the process stopped or the stream closed.</summary>
    Closed,
}

/// <summary>A tool server that cannot be used. Its message never holds a header value.</summary>
public sealed class ToolServerException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ToolServerException()
        : this(ToolServerFailure.Unreachable)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What happened.</param>
    public ToolServerException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What happened.</param>
    /// <param name="innerException">The cause.</param>
    public ToolServerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="failure">The cause.</param>
    /// <param name="innerException">The exception of the adapter.</param>
    public ToolServerException(ToolServerFailure failure, Exception? innerException = null)
        : base($"The tool server cannot be used: {failure}.", innerException)
    {
        Failure = failure;
    }

    /// <summary>The cause.</summary>
    public ToolServerFailure Failure { get; }
}
