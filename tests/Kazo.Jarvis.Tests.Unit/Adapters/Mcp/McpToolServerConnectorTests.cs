using System.IO.Pipelines;
using System.Net;
using System.Text.Json;
using Kazo.Jarvis.Adapters.Mcp;
using Kazo.Jarvis.Kernel.Tools.Servers;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Mcp;

/// <summary>
/// The MCP client (ADR 0018, sections 1 and 2) against a real server of the same package, in memory: the tools as
/// the server describes them, a call, and the named cause of each failure.
/// </summary>
[TestFixture]
public sealed class McpToolServerConnectorTests
{
    private static JsonElement Arguments(object value) => JsonSerializer.SerializeToElement(value);

    [Test]
    public async Task The_tools_are_listed_as_the_server_describes_them_and_a_call_returns_its_text()
    {
        await using var server = new InMemoryServer();
        await using var connection = await McpToolServerConnector.ConnectAsync(server.ClientTransport, "test", CancellationToken.None);

        var tools = await connection.ListToolsAsync();
        var add = tools.Single(tool => tool.Name == "add");
        var erase = tools.Single(tool => tool.Name == "erase");
        Assert.Multiple(() =>
        {
            Assert.That(connection.AnnouncedName, Is.EqualTo("in-memory"));
            Assert.That(add.Description, Is.EqualTo("Adds two numbers."));
            Assert.That(add.ReadOnly, Is.True);
            Assert.That(add.Schema.GetProperty("properties").TryGetProperty("a", out _), Is.True);
            Assert.That(erase.ReadOnly, Is.False, "Without the annotation, a tool is not a read.");
        });

        var sum = await connection.CallAsync("add", Arguments(new { a = 2, b = 40 }));
        Assert.Multiple(() =>
        {
            Assert.That(sum.IsError, Is.False);
            Assert.That(sum.Text, Is.EqualTo("42"));
        });
    }

    [Test]
    public async Task A_tool_that_fails_or_does_not_exist_is_an_answer_not_a_broken_connection()
    {
        await using var server = new InMemoryServer();
        await using var connection = await McpToolServerConnector.ConnectAsync(server.ClientTransport, "test", CancellationToken.None);

        var failed = await connection.CallAsync("erase", Arguments(new { }));
        var unknown = await connection.CallAsync("no_such_tool", Arguments(new { }));
        var again = await connection.CallAsync("add", Arguments(new { a = 1, b = 1 }));
        Assert.Multiple(() =>
        {
            Assert.That(failed.IsError, Is.True);
            Assert.That(unknown.IsError, Is.True);
            Assert.That(again.Text, Is.EqualTo("2"), "The connection still works.");
        });
    }

    [Test]
    public async Task The_end_of_the_server_completes_the_connection_and_a_call_then_names_it()
    {
        var server = new InMemoryServer();
        await using var connection = await McpToolServerConnector.ConnectAsync(server.ClientTransport, "test", CancellationToken.None);
        Assert.That(connection.Closed.IsCompleted, Is.False);

        await server.DisposeAsync();
        await connection.Closed.WaitAsync(TimeSpan.FromSeconds(5));
        var failure = Assert.ThrowsAsync<ToolServerException>(async () => await connection.CallAsync("add", Arguments(new { a = 1, b = 1 })));
        Assert.That(failure!.Failure, Is.EqualTo(ToolServerFailure.Closed).Or.EqualTo(ToolServerFailure.Unreachable));
    }

    [Test]
    public void A_command_that_does_not_exist_is_named()
    {
        var connector = new McpToolServerConnector();
        var failure = Assert.ThrowsAsync<ToolServerException>(async () =>
            await connector.ConnectAsync(new ToolServerEndpoint { Name = "missing", Command = "kazo-jarvis-no-such-program-" + Guid.NewGuid().ToString("N") }));
        Assert.That(failure!.Failure, Is.EqualTo(ToolServerFailure.CommandNotFound));
    }

    [Test]
    public void A_command_is_looked_up_in_the_folders_of_the_path_with_the_extensions_of_windows()
    {
        var folder = Directory.CreateTempSubdirectory("jarvis-mcp-path-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "npx.cmd"), string.Empty);
            File.WriteAllText(Path.Combine(folder, "server"), string.Empty);
            var path = string.Join(Path.PathSeparator, Path.Combine(folder, "missing"), folder);
            Assert.Multiple(() =>
            {
                // Windows: « npx » is npx.cmd, found through PATHEXT; a name that already ends with an extension of PATHEXT matches it whatever the case. Lower case extensions here, since the Linux file system is case-sensitive.
                Assert.That(McpToolServerConnector.CommandExists("npx", path, ".com;.exe;.bat;.cmd"), Is.True);
                Assert.That(McpToolServerConnector.CommandExists("npx.cmd", path, ".EXE;.CMD"), Is.True);
                Assert.That(McpToolServerConnector.CommandExists("uvx", path, ".COM;.EXE;.BAT;.CMD"), Is.False);

                // Elsewhere, the name as it is.
                Assert.That(McpToolServerConnector.CommandExists("server", path, null), Is.True);
                Assert.That(McpToolServerConnector.CommandExists("npx", path, null), Is.False);

                // A path is checked as a file, not looked up.
                Assert.That(McpToolServerConnector.CommandExists(Path.Combine(folder, "server"), null, null), Is.True);
                Assert.That(McpToolServerConnector.CommandExists(Path.Combine(folder, "npx"), null, ".cmd"), Is.True);
                Assert.That(McpToolServerConnector.CommandExists(Path.Combine(folder, "nothing"), path, ".CMD"), Is.False);
                Assert.That(McpToolServerConnector.CommandExists("  ", path, null), Is.False);
            });
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestCase(HttpStatusCode.Unauthorized, ToolServerFailure.Rejected)]
    [TestCase(HttpStatusCode.Forbidden, ToolServerFailure.Rejected)]
    [TestCase(HttpStatusCode.NotFound, ToolServerFailure.Protocol)]
    public void An_http_refusal_is_named(HttpStatusCode status, ToolServerFailure expected)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status));
        var connector = new McpToolServerConnector(() => handler);
        var failure = Assert.ThrowsAsync<ToolServerException>(async () =>
            await connector.ConnectAsync(new ToolServerEndpoint
            {
                Name = "remote",
                Address = new Uri("https://tools.example/mcp"),
                Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer s3cret-token" },
            }));
        Assert.Multiple(() =>
        {
            Assert.That(failure!.Failure, Is.EqualTo(expected));
            Assert.That(handler.Authorizations, Has.All.EqualTo("Bearer s3cret-token"), "The header of the vault is sent with every request.");
            Assert.That(handler.Authorizations, Is.Not.Empty);
            Assert.That(failure.ToString(), Does.Not.Contain("s3cret-token"));
        });
    }

    [Test]
    public void A_server_that_cannot_be_reached_is_named()
    {
        var connector = new McpToolServerConnector(() => new StubHandler(_ => throw new HttpRequestException("No such host.", new System.Net.Sockets.SocketException(11001))));
        var failure = Assert.ThrowsAsync<ToolServerException>(async () =>
            await connector.ConnectAsync(new ToolServerEndpoint { Name = "remote", Address = new Uri("https://tools.example/mcp") }));
        Assert.That(failure!.Failure, Is.EqualTo(ToolServerFailure.Unreachable));
    }

    [Test]
    public void A_web_page_instead_of_a_tool_server_is_named()
    {
        var connector = new McpToolServerConnector(() => new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>Hello</body></html>", System.Text.Encoding.UTF8, "text/html"),
        }));
        var failure = Assert.ThrowsAsync<ToolServerException>(async () =>
            await connector.ConnectAsync(new ToolServerEndpoint { Name = "remote", Address = new Uri("https://tools.example/") }));
        Assert.That(failure!.Failure, Is.EqualTo(ToolServerFailure.Protocol));
    }

    [Test]
    public void The_exceptions_of_the_network_map_to_named_causes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(McpToolServerConnector.FailureOf(new TimeoutException(), byCommand: false, connecting: true), Is.EqualTo(ToolServerFailure.TimedOut));
            Assert.That(McpToolServerConnector.FailureOf(new IOException("x", new System.ComponentModel.Win32Exception(2)), byCommand: true, connecting: true), Is.EqualTo(ToolServerFailure.CommandNotFound));
            Assert.That(McpToolServerConnector.FailureOf(new IOException("pipe closed"), byCommand: true, connecting: false), Is.EqualTo(ToolServerFailure.Closed));
            Assert.That(McpToolServerConnector.FailureOf(new JsonException(), byCommand: false, connecting: false), Is.EqualTo(ToolServerFailure.Protocol));
            Assert.That(McpToolServerConnector.FailureOf(new InvalidOperationException(), byCommand: false, connecting: false), Is.EqualTo(ToolServerFailure.Unreachable));
        });
    }

    /// <summary>A server of the package that speaks over two pipes in memory.</summary>
    private sealed class InMemoryServer : IAsyncDisposable
    {
        private readonly Pipe toServer = new();
        private readonly Pipe toClient = new();
        private readonly McpServer server;
        private readonly CancellationTokenSource stop = new();
        private readonly Task running;

        public InMemoryServer()
        {
            var options = new McpServerOptions
            {
                ServerInfo = new Implementation { Name = "in-memory", Version = "1.0" },
                ToolCollection =
                [
                    McpServerTool.Create((int a, int b) => (a + b).ToString(System.Globalization.CultureInfo.InvariantCulture), new McpServerToolCreateOptions { Name = "add", Description = "Adds two numbers.", ReadOnly = true }),
                    McpServerTool.Create(string () => throw new InvalidOperationException("Nothing to erase."), new McpServerToolCreateOptions { Name = "erase", Description = "Erases everything." }),
                ],
            };
            server = McpServer.Create(new StreamServerTransport(toServer.Reader.AsStream(), toClient.Writer.AsStream(), "in-memory"), options);
            running = server.RunAsync(stop.Token);
            ClientTransport = new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream());
        }

        public IClientTransport ClientTransport { get; }

        public async ValueTask DisposeAsync()
        {
            await stop.CancelAsync();
            await server.DisposeAsync();
            await toClient.Writer.CompleteAsync();
            await toServer.Writer.CompleteAsync();
            try
            {
                await running;
            }
            catch (OperationCanceledException)
            {
            }

            stop.Dispose();
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string?> Authorizations { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Authorizations)
            {
                Authorizations.Add(request.Headers.TryGetValues("Authorization", out var values) ? string.Join(",", values) : null);
            }

            return Task.FromResult(answer(request));
        }
    }
}
