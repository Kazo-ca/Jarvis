using System.Net;
using System.Text.Json;
using Kazo.Jarvis.Adapters.LocalServer;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.LocalServer;
using Kazo.Jarvis.Tests.Unit.Adapters.Secrets;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Kazo.Jarvis.Tests.Unit.Adapters.LocalServer;

/// <summary>
/// The MCP server of Jarvis on <c>/mcp</c> (ADR 0021, sections 3 and 4), called by a real MCP client as Claude
/// Code does: the <c>approve</c> tool hands the request over with the task of the session, answers in the form
/// spike E12 proved, gives the input back unchanged, and is closed to the token of the user's hooks.
/// </summary>
[TestFixture]
public sealed class KestrelApproveToolTests
{
    private const string UserToken = "user-hook-token";

    private TemporaryFolder folder = null!;
    private FakeVault vault = null!;
    private FakeApprovals approvals = null!;
    private KestrelLocalServer server = null!;

    [SetUp]
    public async Task SetUp()
    {
        folder = new TemporaryFolder();
        vault = new FakeVault();
        vault.Secrets[LocalServerOptions.UserHookToken.Value] = UserToken;
        approvals = new FakeApprovals();
        server = new KestrelLocalServer(new JsonFileSettingsStore(folder.Paths, [], new FakeTimeProvider()), [], vault, null, fixedPort: 0, approvals);
        await server.StartAsync(CancellationToken.None);
    }

    [TearDown]
    public async Task TearDown()
    {
        await server.DisposeAsync();
        folder.Dispose();
    }

    [Test]
    public async Task The_server_offers_the_approve_tool_and_nothing_else()
    {
        await using var client = await ConnectAsync(server.SessionToken, "task-1");

        var tools = await client.ListToolsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(tools.Select(tool => tool.Name), Is.EqualTo(new[] { "approve" }));
            Assert.That(client.ServerInfo?.Name, Is.EqualTo("jarvis"));
        });
    }

    [Test]
    public async Task An_allowed_request_goes_back_with_its_input_unchanged()
    {
        await using var client = await ConnectAsync(server.SessionToken, "task-1");

        var answer = await CallAsync(client, "Write", """{"file_path":"C:\\src\\notes.txt","content":"é\n"}""", "toolu_1");

        var asked = approvals.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(asked.TaskId, Is.EqualTo("task-1"));
            Assert.That(asked.ToolName, Is.EqualTo("Write"));
            Assert.That(asked.ToolUseId, Is.EqualTo("toolu_1"));
            Assert.That(asked.Input.GetProperty("file_path").GetString(), Is.EqualTo(@"C:\src\notes.txt"));
            Assert.That(answer.GetProperty("behavior").GetString(), Is.EqualTo("allow"));
            Assert.That(answer.GetProperty("updatedInput").GetProperty("file_path").GetString(), Is.EqualTo(@"C:\src\notes.txt"));
            Assert.That(answer.GetProperty("updatedInput").GetProperty("content").GetString(), Is.EqualTo("é\n"));
        });
    }

    [Test]
    public async Task A_denied_request_tells_the_cause_to_the_session()
    {
        approvals.Answer = ClaudeApproval.Deny("Refusé par l'utilisateur.");
        await using var client = await ConnectAsync(server.SessionToken, "task-1");

        var answer = await CallAsync(client, "Bash", """{"command":"rm -rf ."}""");

        Assert.Multiple(() =>
        {
            Assert.That(answer.GetProperty("behavior").GetString(), Is.EqualTo("deny"));
            Assert.That(answer.GetProperty("message").GetString(), Is.EqualTo("Refusé par l'utilisateur."));
            Assert.That(answer.TryGetProperty("updatedInput", out _), Is.False);
            Assert.That(approvals.Requests.Single().ToolUseId, Is.Null);
        });
    }

    [Test]
    public async Task A_decision_that_fails_is_a_denial()
    {
        approvals.Failure = new InvalidOperationException("secret detail");
        await using var client = await ConnectAsync(server.SessionToken, "task-1");

        var answer = await CallAsync(client, "Write", "{}");

        Assert.Multiple(() =>
        {
            Assert.That(answer.GetProperty("behavior").GetString(), Is.EqualTo("deny"));
            Assert.That(answer.GetProperty("message").GetString(), Does.Not.Contain("secret"));
        });
    }

    [Test]
    public async Task Without_a_task_header_the_request_arrives_without_a_task()
    {
        await using var client = await ConnectAsync(server.SessionToken, null);

        await CallAsync(client, "Write", "{}");

        Assert.That(approvals.Requests.Single().TaskId, Is.Null, "The slice denies a request it cannot tie to a task.");
    }

    [Test]
    public async Task Without_someone_to_decide_every_request_is_denied()
    {
        await using var alone = new KestrelLocalServer(new JsonFileSettingsStore(folder.Paths, [], new FakeTimeProvider()), [], vault, null, fixedPort: 0);
        await alone.StartAsync(CancellationToken.None);
        await using var client = await ConnectAsync(alone.SessionToken, "task-1", alone.Status.Port);

        var answer = await CallAsync(client, "Write", "{}");

        Assert.That(answer.GetProperty("behavior").GetString(), Is.EqualTo("deny"));
    }

    [Test]
    public async Task The_MCP_server_is_closed_without_the_token_of_the_run()
    {
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{server.Status.Port}") };
        using var none = await http.PostAsync("/mcp", new StringContent("{}"));
        using var asUser = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent("{}") };
        asUser.Headers.Authorization = new("Bearer", UserToken);
        using var user = await http.SendAsync(asUser);

        Assert.Multiple(() =>
        {
            Assert.That(none.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(user.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "The token written in the user's settings never reaches the permission tool.");
            Assert.That(approvals.Requests, Is.Empty);
        });
    }

    private Task<McpClient> ConnectAsync(string token, string? task, int? port = null)
    {
        var headers = new Dictionary<string, string> { ["Authorization"] = "Bearer " + token };
        if (task is not null)
        {
            headers[LocalRequest.TaskHeader] = task;
        }

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri($"http://127.0.0.1:{port ?? server.Status.Port}/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = headers,
        });
        return McpClient.CreateAsync(transport);
    }

    private static async Task<JsonElement> CallAsync(McpClient client, string tool, string input, string? toolUseId = null)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["tool_name"] = tool,
            ["input"] = JsonDocument.Parse(input).RootElement.Clone(),
        };
        if (toolUseId is not null)
        {
            arguments["tool_use_id"] = toolUseId;
        }

        var result = await client.CallToolAsync("approve", arguments);
        Assert.That(result.IsError, Is.Not.True);
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private sealed class FakeApprovals : IClaudeApprovals
    {
        public List<ClaudeApprovalRequest> Requests { get; } = [];

        public ClaudeApproval Answer { get; set; } = ClaudeApproval.Allow;

        public Exception? Failure { get; set; }

        public ValueTask<ClaudeApproval> AskAsync(ClaudeApprovalRequest request, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Requests.Add(request);
            return ValueTask.FromResult(Answer);
        }
    }
}
