using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Kazo.Jarvis.Kernel.Tools.Servers;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Kazo.Jarvis.Adapters.Mcp;

/// <summary>
/// The tool servers through the official <c>ModelContextProtocol</c> package (ADR 0018, section 1; spike E14). By
/// address: streamable HTTP, with the fall-back to SSE the package detects itself. By command: the process speaks on
/// its standard input and output, and is stopped when the connection is disposed.
/// <para>
/// The client declares no capability: a server that asks it to run a model (<c>sampling</c>) or to question the
/// user (<c>elicitation</c>) is refused by the protocol (ADR 0018, section 3). Prompts and resources are not read.
/// </para>
/// </summary>
public sealed class McpToolServerConnector : IToolServerConnector
{
    /// <summary>The longest wait for the opening exchange.</summary>
    internal static readonly TimeSpan ConnectionDeadline = TimeSpan.FromSeconds(20);

    private readonly Func<HttpMessageHandler>? handlerFactory;

    /// <summary>Creates the connector.</summary>
    public McpToolServerConnector()
    {
    }

    /// <summary>Creates the connector with a chosen HTTP handler, for tests.</summary>
    /// <param name="handlerFactory">Creates the handler of one connection.</param>
    internal McpToolServerConnector(Func<HttpMessageHandler> handlerFactory)
    {
        this.handlerFactory = handlerFactory;
    }

    /// <inheritdoc />
    public async ValueTask<IToolServerConnection> ConnectAsync(ToolServerEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (endpoint.Address is { } address)
        {
            // One HttpClient per connection: its default headers hold the token of this server only.
            var http = handlerFactory is null ? new HttpClient() : new HttpClient(handlerFactory(), disposeHandler: true);
            try
            {
                var transport = new HttpClientTransport(
                    new HttpClientTransportOptions
                    {
                        Endpoint = address,
                        Name = endpoint.Name,
                        TransportMode = HttpTransportMode.AutoDetect,
                        ConnectionTimeout = ConnectionDeadline,
                        AdditionalHeaders = new Dictionary<string, string>(endpoint.Headers, StringComparer.OrdinalIgnoreCase),
                    },
                    http,
                    loggerFactory: null,
                    ownsHttpClient: true);
                return await ConnectAsync(transport, endpoint.Name, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                http.Dispose();
                throw;
            }
        }

        if (string.IsNullOrWhiteSpace(endpoint.Command))
        {
            throw new ToolServerException(ToolServerFailure.CommandNotFound);
        }

        var stdio = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = endpoint.Command,
            Arguments = [.. endpoint.Arguments],
            Name = endpoint.Name,
        });
        return await ConnectAsync(stdio, endpoint.Name, cancellationToken, byCommand: true).ConfigureAwait(false);
    }

    /// <summary>Does the opening exchange over a transport. Internal so that a test can give a transport in memory.</summary>
    /// <param name="transport">The transport.</param>
    /// <param name="name">The name of the server in the settings.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <param name="byCommand">Whether the transport starts a process.</param>
    /// <returns>The open connection.</returns>
    internal static async Task<IToolServerConnection> ConnectAsync(IClientTransport transport, string name, CancellationToken cancellationToken, bool byCommand = false)
    {
        var options = new McpClientOptions
        {
            ClientInfo = new Implementation { Name = "Kazo.Jarvis", Version = "1.0" },
            InitializationTimeout = ConnectionDeadline,
        };
        try
        {
            var client = await McpClient.CreateAsync(transport, options, loggerFactory: null, cancellationToken).ConfigureAwait(false);
            return new Connection(client);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ToolServerException(FailureOf(exception, byCommand, connecting: true), exception);
        }
    }

    /// <summary>The named cause of an exception of the package or of the network (ADR 0018, section 2).</summary>
    /// <param name="exception">What was thrown.</param>
    /// <param name="byCommand">Whether the server is started by command.</param>
    /// <param name="connecting">Whether the opening exchange was under way.</param>
    /// <returns>The cause.</returns>
    internal static ToolServerFailure FailureOf(Exception exception, bool byCommand, bool connecting)
    {
        // An exception of the package with nothing more precise inside: what answered is not a tool server.
        var fromProtocol = false;
        for (var current = exception; current is not null; current = current.InnerException)
        {
            fromProtocol |= current is McpException;
            switch (current)
            {
                case ToolServerException known:
                    return known.Failure;
                case HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden }:
                    return ToolServerFailure.Rejected;
                case HttpRequestException { StatusCode: not null }:
                    // Something answered over HTTP, but not a tool server: a wrong path, a web page.
                    return ToolServerFailure.Protocol;
                case HttpRequestException or SocketException:
                    return ToolServerFailure.Unreachable;
                case TimeoutException or OperationCanceledException:
                    return ToolServerFailure.TimedOut;
                case Win32Exception or FileNotFoundException or DirectoryNotFoundException:
                    return ToolServerFailure.CommandNotFound;
                case ClientTransportClosedException:
                    return ToolServerFailure.Closed;
                case JsonException or McpProtocolException:
                    return ToolServerFailure.Protocol;
                default:
                    break;
            }
        }

        if (fromProtocol)
        {
            return ToolServerFailure.Protocol;
        }

        // A process that could not start or that died during the opening exchange: for the user, the command is
        // what to look at. Anything else left is an end of the stream.
        if (byCommand && connecting)
        {
            return ToolServerFailure.CommandNotFound;
        }

        return exception is IOException ? ToolServerFailure.Closed : byCommand ? ToolServerFailure.Closed : ToolServerFailure.Unreachable;
    }

    private sealed class Connection(McpClient client) : IToolServerConnection
    {
        public string? AnnouncedName => client.ServerInfo?.Name;

        public Task Closed => client.Completion;

        public async ValueTask<IReadOnlyList<ToolServerTool>> ListToolsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var tools = await client.ListToolsAsync(options: null, cancellationToken).ConfigureAwait(false);
                return [.. tools.Select(tool => new ToolServerTool(
                    tool.ProtocolTool.Name,
                    tool.ProtocolTool.Description ?? string.Empty,
                    tool.ProtocolTool.InputSchema.Clone(),
                    tool.ProtocolTool.Annotations?.ReadOnlyHint == true))];
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new ToolServerException(FailureOf(exception, byCommand: false, connecting: false), exception);
            }
        }

        public async ValueTask<ToolServerResult> CallAsync(string tool, JsonElement arguments, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tool);
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (arguments.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in arguments.EnumerateObject())
                {
                    values[property.Name] = property.Value.Clone();
                }
            }

            try
            {
                var result = await client.CallToolAsync(tool, values, progress: null, options: null, cancellationToken).ConfigureAwait(false);
                var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
                if (text.Length == 0 && result.StructuredContent is { } structured)
                {
                    text = structured.ToString();
                }

                return new ToolServerResult(text, result.IsError == true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (McpProtocolException exception)
            {
                // The server answered with a protocol error (unknown tool, wrong arguments): an answer of the tool
                // for the model, not a broken connection.
                return new ToolServerResult(exception.Message, IsError: true);
            }
            catch (Exception exception)
            {
                throw new ToolServerException(FailureOf(exception, byCommand: false, connecting: false), exception);
            }
        }

        public ValueTask DisposeAsync() => client.DisposeAsync();
    }
}
