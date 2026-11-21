using System.Text.Json;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Mcp.Servers;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Kernel.Tools.Servers;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Adapters.Secrets;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.Mcp;

/// <summary>
/// The tool servers (ADR 0018, sections 1 to 4): nothing configured at the start, every tool off when it is found,
/// an approval tied to what the tool says it does, a class the user can only raise, and a server that cannot be
/// reached that only loses its tools.
/// </summary>
[TestFixture]
public sealed class McpServerHubTests
{
    private static readonly JsonElement NoParameters = JsonSerializer.SerializeToElement(new { type = "object" });

    private TemporaryFolder folder = null!;
    private ISettingsStore settings = null!;
    private ToolRegistry registry = null!;
    private FakeVault vault = null!;
    private FakeConnector connector = null!;
    private FakeTimeProvider time = null!;
    private McpServerHub hub = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        registry = new ToolRegistry([new BuiltIn("get_datetime")], settings);
        vault = new FakeVault();
        connector = new FakeConnector();
        time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        hub = new McpServerHub(settings, registry, vault, connector, time);
    }

    [TearDown]
    public async Task TearDown()
    {
        await hub.DisposeAsync();
        registry.Dispose();
        folder.Dispose();
    }

    private static McpServerOptions ByAddress(string name = "home", string address = "https://tools.example/mcp") => new() { Name = name, Address = address };

    private static McpServerOptions ByCommand(string name = "local") => new() { Name = name, Command = "tool-server", Arguments = ["--stdio"] };

    private static ToolServerTool Tool(string name, string description = "Does something.", bool readOnly = false) => new(name, description, NoParameters, readOnly);

    private async Task StartWithAsync(params McpServerOptions[] servers)
    {
        foreach (var server in servers)
        {
            Assert.That((await hub.AddAsync(server)).Outcome, Is.EqualTo(SettingsWriteOutcome.Saved));
        }

        await hub.StartAsync(CancellationToken.None);
    }

    private McpServerStatus Status(string name = "home") => hub.Statuses.Single(status => status.Options.Name == name);

    private Task TurnOnAsync(string tool) =>
        settings.WriteAsync(ToolActivation.Section, settings.Read(ToolActivation.Section).With(tool, true)).AsTask();

    [Test]
    public async Task No_server_is_configured_at_the_start_and_nothing_connects()
    {
        await hub.StartAsync(CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(hub.Statuses, Is.Empty);
            Assert.That(connector.Connections, Is.Zero);
            Assert.That(registry.Tools.Select(tool => tool.Name), Is.EqualTo(new[] { "get_datetime" }));
        });
    }

    [Test]
    public async Task The_tools_of_a_server_enter_the_registry_as_they_are_and_all_off()
    {
        connector.Tools = [Tool("lookup", "Looks something up.", readOnly: true), Tool("send", "Sends a message.")];
        await StartWithAsync(ByAddress());
        await WaitUntil(() => Status().State == McpServerState.Connected);

        var lookup = registry.Find("lookup")!;
        var send = registry.Find("send")!;
        Assert.Multiple(() =>
        {
            Assert.That(lookup.Origin, Is.EqualTo("mcp:home"));
            Assert.That(lookup.Describe(AssistantLanguage.French), Is.EqualTo("Looks something up."), "The description of the server, as it is.");
            Assert.That(lookup.EnabledByDefault, Is.False);
            Assert.That(registry.Enabled.Select(tool => tool.Name), Is.EqualTo(new[] { "get_datetime" }), "Every tool of a server is off when it is found.");
            Assert.That(lookup.DeclaredClass, Is.EqualTo(ActionClass.Read), "A declared read is a read.");
            Assert.That(send.DeclaredClass, Is.EqualTo(ActionClass.Sensitive), "Everything else asks for a confirmation.");
            Assert.That(send.Discloses(AssistantLanguage.French), Does.Contain("https://tools.example/mcp"));
            Assert.That(Status().Tools.Select(tool => tool.Name), Is.EqualTo(new[] { "lookup", "send" }));
        });
    }

    [Test]
    public async Task The_user_can_raise_the_class_of_a_tool_and_never_lower_it()
    {
        connector.Tools = [Tool("lookup", readOnly: true), Tool("send")];
        await StartWithAsync(ByAddress());
        await WaitUntil(() => Status().State == McpServerState.Connected);

        await hub.RaiseClassAsync("home", "lookup", ActionClass.Critical);
        Assert.That(registry.Find("lookup")!.Classify(default), Is.EqualTo(ActionClass.Critical));

        await hub.RaiseClassAsync("home", "lookup", null);
        Assert.That(registry.Find("lookup")!.Classify(default), Is.EqualTo(ActionClass.Read));

        // « Lecture » on a tool the server does not declare read-only: refused by the hub and by the settings.
        await hub.RaiseClassAsync("home", "send", ActionClass.Read);
        var forced = await hub.ChangeAsync("home", server => server with { Classes = new Dictionary<string, ActionClass> { ["send"] = ActionClass.Read } });
        Assert.Multiple(() =>
        {
            Assert.That(forced.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
            Assert.That(registry.Find("send")!.Classify(default), Is.EqualTo(ActionClass.Sensitive));
        });
    }

    [Test]
    public async Task Turning_a_tool_on_keeps_the_fingerprint_and_a_changed_tool_is_turned_off()
    {
        connector.Tools = [Tool("send", "Sends a message to the family.")];
        await StartWithAsync(ByAddress());
        await WaitUntil(() => Status().State == McpServerState.Connected);

        await TurnOnAsync("send");
        await WaitUntil(() => settings.Read(McpServersOptions.Section).Find("home")!.Approved.ContainsKey("send"));
        Assert.That(registry.Enabled.Select(tool => tool.Name), Does.Contain("send"));

        // The same tool after a new connection: still on.
        await hub.RetryAsync("home");
        Assert.That(registry.Enabled.Select(tool => tool.Name), Does.Contain("send"));

        // The server now says something else of the same tool.
        connector.Tools = [Tool("send", "Sends a message to anyone and reads the contacts.")];
        await hub.RetryAsync("home");
        var status = Status().Tools.Single();
        Assert.Multiple(() =>
        {
            Assert.That(registry.Enabled.Select(tool => tool.Name), Does.Not.Contain("send"), "A server cannot change in silence what an approved tool says it does.");
            Assert.That(status.Enabled, Is.False);
            Assert.That(status.ChangedSinceApproval, Is.True);
        });

        // The user reads it again and turns it back on: the new fingerprint is the approved one.
        await TurnOnAsync("send");
        await WaitUntil(() => !Status().Tools.Single().ChangedSinceApproval);
        Assert.That(registry.Enabled.Select(tool => tool.Name), Does.Contain("send"));
    }

    [Test]
    public async Task A_switch_left_on_without_an_approval_is_turned_off_at_discovery()
    {
        // A switch written by hand, or left by a removed server of the same name.
        await TurnOnAsync("send");
        connector.Tools = [Tool("send")];
        await StartWithAsync(ByAddress());
        await WaitUntil(() => Status().State == McpServerState.Connected);

        Assert.That(registry.Enabled.Select(tool => tool.Name), Does.Not.Contain("send"));
    }

    [Test]
    public async Task A_name_already_taken_is_prefixed_by_the_server()
    {
        connector.Tools = [Tool("get_datetime"), Tool("weather.now")];
        await StartWithAsync(ByAddress("my-home"));
        await WaitUntil(() => Status("my-home").State == McpServerState.Connected);

        Assert.Multiple(() =>
        {
            Assert.That(registry.Find("get_datetime")!.Origin, Is.EqualTo(ToolOrigins.BuiltIn));
            Assert.That(registry.Find("my_home__get_datetime")!.Origin, Is.EqualTo("mcp:my-home"));
            Assert.That(registry.Find("weather_now"), Is.Not.Null, "A character a model does not accept in a name is replaced.");
        });
    }

    [Test]
    public async Task The_token_goes_to_the_vault_and_to_the_server_never_to_the_settings_file()
    {
        connector.Tools = [Tool("lookup", readOnly: true)];
        var server = ByAddress() with { Headers = ["Authorization"] };
        await hub.AddAsync(server, new Dictionary<string, string> { ["Authorization"] = "Bearer s3cret-token" });
        await hub.StartAsync(CancellationToken.None);
        await WaitUntil(() => Status().State == McpServerState.Connected);

        Assert.Multiple(() =>
        {
            Assert.That(vault.Secrets["mcp.home.header.Authorization"], Is.EqualTo("Bearer s3cret-token"));
            Assert.That(connector.LastEndpoint!.Headers["Authorization"], Is.EqualTo("Bearer s3cret-token"));
            Assert.That(File.ReadAllText(folder.Paths.ConfigFile), Does.Not.Contain("s3cret-token"));
            Assert.That(connector.LastEndpoint.ToString(), Does.Not.Contain("s3cret-token"));
        });

        await hub.RemoveAsync("home");
        await WaitUntil(() => registry.Find("lookup") is null);
        Assert.That(vault.Secrets, Is.Empty, "Removing a server removes its secrets.");
    }

    [Test]
    public async Task A_refused_server_leaves_no_secret_behind()
    {
        var result = await hub.AddAsync(new McpServerOptions { Name = "Bad Name", Address = "https://tools.example", Headers = ["Authorization"] },
            new Dictionary<string, string> { ["Authorization"] = "Bearer x" });
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
            Assert.That(vault.Secrets, Is.Empty);
        });
    }

    [Test]
    public async Task A_server_that_cannot_be_reached_is_tried_again_after_5_seconds_then_twice_as_long()
    {
        connector.Tools = [Tool("lookup", readOnly: true)];
        connector.FailWith = ToolServerFailure.Unreachable;
        await StartWithAsync(ByAddress());
        await WaitUntil(() => Status().State == McpServerState.Failed);
        Assert.Multiple(() =>
        {
            Assert.That(Status().Failure, Is.EqualTo(ToolServerFailure.Unreachable));
            Assert.That(registry.Find("lookup"), Is.Null);
            Assert.That(connector.Connections, Is.EqualTo(1));
        });

        time.Advance(TimeSpan.FromSeconds(4));
        await Task.Delay(50);
        Assert.That(connector.Connections, Is.EqualTo(1), "Not before 5 seconds.");

        time.Advance(TimeSpan.FromSeconds(1));
        await WaitUntil(() => connector.Connections == 2);
        await WaitUntil(() => Status().State == McpServerState.Failed);

        time.Advance(TimeSpan.FromSeconds(9));
        await Task.Delay(50);
        Assert.That(connector.Connections, Is.EqualTo(2), "The second wait is 10 seconds.");

        connector.FailWith = null;
        time.Advance(TimeSpan.FromSeconds(1));
        await WaitUntil(() => Status().State == McpServerState.Connected);
        Assert.That(registry.Find("lookup"), Is.Not.Null);
    }

    [Test]
    public async Task A_network_error_is_tried_once_more_and_a_second_failure_takes_the_tools_away()
    {
        connector.Tools = [Tool("lookup", readOnly: true)];
        await StartWithAsync(ByAddress());
        await WaitUntil(() => Status().State == McpServerState.Connected);
        var tool = registry.Find("lookup")!;

        connector.CallFailures = 1;
        Assert.That(await tool.InvokeAsync(default, CancellationToken.None), Is.EqualTo("lookup: ok"));
        Assert.That(connector.Connections, Is.EqualTo(1), "Tried again on the same connection (spike E14).");

        connector.CallFailures = 2;
        var failure = Assert.ThrowsAsync<ToolException>(async () => await tool.InvokeAsync(default, CancellationToken.None));
        Assert.Multiple(() =>
        {
            Assert.That(failure!.Message, Does.Contain("'home'").And.Contain("cannot be reached"));
            Assert.That(registry.Find("lookup"), Is.Null, "The tools of a server that is gone leave the list given to the model.");
            Assert.That(Status().State, Is.EqualTo(McpServerState.Failed));
        });
    }

    [Test]
    public async Task A_failure_reported_by_the_tool_is_returned_to_the_model_and_keeps_the_connection()
    {
        connector.Tools = [Tool("lookup", readOnly: true)];
        connector.ToolError = "No such symbol.";
        await StartWithAsync(ByAddress());
        await WaitUntil(() => Status().State == McpServerState.Connected);

        var failure = Assert.ThrowsAsync<ToolException>(async () => await registry.Find("lookup")!.InvokeAsync(default, CancellationToken.None));
        Assert.Multiple(() =>
        {
            Assert.That(failure!.Message, Is.EqualTo("No such symbol."));
            Assert.That(Status().State, Is.EqualTo(McpServerState.Connected));
        });
    }

    [Test]
    public async Task A_command_that_stops_keeps_its_tools_and_is_started_again_three_times_an_hour_at_most()
    {
        connector.Tools = [Tool("roll", readOnly: true)];
        await StartWithAsync(ByCommand());
        await WaitUntil(() => Status("local").State == McpServerState.Connected);
        var tool = registry.Find("roll")!;
        Assert.That(tool.Discloses(AssistantLanguage.English), Does.Contain("tool-server"));

        for (var restart = 1; restart <= McpServerSession.RestartsAnHour; restart++)
        {
            connector.Last!.Close();
            await WaitUntil(() => Status("local").State == McpServerState.Stopped);
            Assert.That(registry.Find("roll"), Is.Not.Null, "The tools stay: the next call starts the program again.");

            Assert.That(await tool.InvokeAsync(default, CancellationToken.None), Is.EqualTo("roll: ok"));
            Assert.That(connector.Connections, Is.EqualTo(1 + restart));
            tool = registry.Find("roll")!;
        }

        connector.Last!.Close();
        await WaitUntil(() => Status("local").State == McpServerState.Stopped);
        Assert.ThrowsAsync<ToolException>(async () => await tool.InvokeAsync(default, CancellationToken.None));
        Assert.That(connector.Connections, Is.EqualTo(1 + McpServerSession.RestartsAnHour), "Not a fourth time in the hour.");

        time.Advance(TimeSpan.FromHours(1));
        Assert.That(await tool.InvokeAsync(default, CancellationToken.None), Is.EqualTo("roll: ok"));
    }

    [Test]
    public async Task A_command_that_does_not_start_is_not_tried_again_by_itself()
    {
        connector.FailWith = ToolServerFailure.CommandNotFound;
        await StartWithAsync(ByCommand());
        await WaitUntil(() => Status("local").State == McpServerState.Failed);

        time.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(50);
        Assert.Multiple(() =>
        {
            Assert.That(connector.Connections, Is.EqualTo(1));
            Assert.That(Status("local").Failure, Is.EqualTo(ToolServerFailure.CommandNotFound));
        });
    }

    [Test]
    public async Task Turning_a_server_off_closes_it_and_takes_its_tools_away()
    {
        connector.Tools = [Tool("lookup", readOnly: true)];
        await StartWithAsync(ByAddress());
        await WaitUntil(() => Status().State == McpServerState.Connected);
        var connection = connector.Last!;

        await hub.ChangeAsync("home", server => server with { Enabled = false });
        await WaitUntil(() => registry.Find("lookup") is null);
        Assert.Multiple(() =>
        {
            Assert.That(connection.Disposed, Is.True);
            Assert.That(Status().State, Is.EqualTo(McpServerState.Disabled));
        });
    }

    [Test]
    public async Task Stopping_the_hub_closes_every_connection()
    {
        connector.Tools = [Tool("roll", readOnly: true)];
        await StartWithAsync(ByCommand());
        await WaitUntil(() => Status("local").State == McpServerState.Connected);
        var connection = connector.Last!;

        await hub.StopAsync(CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(connection.Disposed, Is.True, "A server started by command is stopped when Jarvis quits.");
            Assert.That(registry.Find("roll"), Is.Null);
        });
    }

    [TestCase(ToolServerFailure.Unreachable)]
    [TestCase(ToolServerFailure.Rejected)]
    [TestCase(ToolServerFailure.TimedOut)]
    [TestCase(ToolServerFailure.CommandNotFound)]
    [TestCase(ToolServerFailure.Protocol)]
    public async Task The_connection_test_names_the_cause(ToolServerFailure failure)
    {
        await hub.AddAsync(ByAddress() with { Enabled = false });
        connector.FailWith = failure;
        Assert.That((await hub.TestAsync("home")).Failure, Is.EqualTo(failure));

        connector.FailWith = null;
        connector.Tools = [Tool("a"), Tool("b")];
        var test = await hub.TestAsync("home");
        Assert.Multiple(() =>
        {
            Assert.That(test.Failure, Is.Null);
            Assert.That(test.Tools, Is.EqualTo(2));
            Assert.That(connector.Last!.Disposed, Is.True, "The test closes its own connection.");
            Assert.That(registry.Find("a"), Is.Null, "A test puts nothing in the registry.");
        });
    }

    [Test]
    public void The_settings_refuse_a_server_that_is_not_well_formed()
    {
        static IReadOnlyList<string> Problems(params McpServerOptions[] servers) => McpServersOptions.Section.Validate(new McpServersOptions { Servers = servers });

        Assert.Multiple(() =>
        {
            Assert.That(Problems(ByAddress(), ByCommand()), Is.Empty);
            Assert.That(Problems(ByAddress("Home")), Is.Not.Empty, "Lower case only: the name is in the origin of the tools and in the secret references.");
            Assert.That(Problems(ByAddress(), ByAddress()), Is.Not.Empty, "Two servers with one name.");
            Assert.That(Problems(new McpServerOptions { Name = "none" }), Is.Not.Empty, "Neither an address nor a command.");
            Assert.That(Problems(ByAddress() with { Command = "x" }), Is.Not.Empty, "Both an address and a command.");
            Assert.That(Problems(ByAddress(address: "ftp://tools.example")), Is.Not.Empty);
            Assert.That(Problems(ByAddress() with { Headers = ["X Bad"] }), Is.Not.Empty);
            Assert.That(Problems(ByAddress() with { Classes = new Dictionary<string, ActionClass> { ["send"] = ActionClass.EverydayEffect } }), Is.Not.Empty);
        });
    }

    [Test]
    public void The_fingerprint_follows_the_name_the_description_the_schema_and_the_read_only_declaration()
    {
        var tool = Tool("send", "Sends.");
        Assert.Multiple(() =>
        {
            Assert.That(McpToolFingerprint.Of(Tool("send", "Sends.")), Is.EqualTo(McpToolFingerprint.Of(tool)));
            Assert.That(McpToolFingerprint.Of(Tool("sends", "Sends.")), Is.Not.EqualTo(McpToolFingerprint.Of(tool)));
            Assert.That(McpToolFingerprint.Of(Tool("send", "Sends more.")), Is.Not.EqualTo(McpToolFingerprint.Of(tool)));
            Assert.That(McpToolFingerprint.Of(Tool("send", "Sends.", readOnly: true)), Is.Not.EqualTo(McpToolFingerprint.Of(tool)));
            Assert.That(McpToolFingerprint.Of(tool with { Schema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { to = new { type = "string" } } }) }), Is.Not.EqualTo(McpToolFingerprint.Of(tool)));
        });
    }

    [TestCase("http://localhost:8123/api/mcp", true)]
    [TestCase("http://192.168.1.20:8123/api/mcp", true)]
    [TestCase("http://10.0.0.5/", true)]
    [TestCase("http://homeassistant.local:8123/", true)]
    [TestCase("http://nas/", true)]
    [TestCase("http://[::1]:3000/", true)]
    [TestCase("https://ha.example.com/api/mcp", false)]
    [TestCase("https://8.8.8.8/", false)]
    [TestCase("https://172.32.0.1/", false)]
    public void A_server_outside_the_local_network_is_told_apart(string address, bool local) =>
        Assert.That(McpServerHub.IsLocal(new Uri(address)), Is.EqualTo(local));

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The condition was not met in 5 s.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class BuiltIn(string name) : ITool
    {
        public string Name => name;

        public string Origin => ToolOrigins.BuiltIn;

        public bool EnabledByDefault => true;

        public ActionClass DeclaredClass => ActionClass.Read;

        public JsonElement Schema => NoParameters;

        public string Describe(AssistantLanguage language) => name;

        public ActionClass Classify(JsonElement arguments) => ActionClass.Read;

        public ValueTask<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken) => ValueTask.FromResult(name);
    }

    [Test]
    public async Task A_server_of_a_kind_that_has_its_own_classification_is_classed_by_it()
    {
        await hub.DisposeAsync();
        var classifier = new FakeClassifier();
        hub = new McpServerHub(settings, registry, vault, connector, time, classifiers: [classifier]);
        connector.Tools = [Tool("turn_on"), Tool("read_state", readOnly: true)];
        await StartWithAsync(ByAddress("maison") with { Kind = McpServerKind.HomeAssistant }, ByAddress("other", "https://other.example/mcp"));
        await WaitUntil(() => registry.Tools.Count(tool => tool.Origin.StartsWith("mcp:", StringComparison.Ordinal)) == 4);

        var home = registry.Tools.Single(tool => tool.Origin == ToolOrigins.Mcp("maison") && tool.Name.EndsWith("turn_on", StringComparison.Ordinal));
        var general = registry.Tools.Single(tool => tool.Origin == ToolOrigins.Mcp("other") && tool.Name.EndsWith("turn_on", StringComparison.Ordinal));
        using var lamp = JsonDocument.Parse("""{"name":"lampe"}""");
        using var doorLock = JsonDocument.Parse("""{"name":"serrure"}""");

        Assert.Multiple(async () =>
        {
            Assert.That(home.DeclaredClass, Is.EqualTo(ActionClass.EverydayEffect), "The lowest class of the kind.");
            Assert.That(await home.ClassifyAsync(lamp.RootElement, CancellationToken.None), Is.EqualTo(ActionClass.EverydayEffect));
            Assert.That(await home.ClassifyAsync(doorLock.RootElement, CancellationToken.None), Is.EqualTo(ActionClass.Critical));
            Assert.That(home.Classify(lamp.RootElement), Is.EqualTo(ActionClass.Sensitive), "Without a question to the server, the general rule.");
            Assert.That(classifier.Asked, Is.EqualTo("maison:read_state: ok"), "The classifier asks the server through the session of the tool.");
            Assert.That(general.DeclaredClass, Is.EqualTo(ActionClass.Sensitive), "A general server keeps the general rule.");
            Assert.That(await general.ClassifyAsync(lamp.RootElement, CancellationToken.None), Is.EqualTo(ActionClass.Sensitive));
        });

        // The user can raise a tool, never lower what the classification finds.
        Assert.That((await hub.RaiseClassAsync("maison", "turn_on", ActionClass.Sensitive)).Outcome, Is.EqualTo(SettingsWriteOutcome.Saved));
        Assert.Multiple(async () =>
        {
            Assert.That(await home.ClassifyAsync(lamp.RootElement, CancellationToken.None), Is.EqualTo(ActionClass.Sensitive));
            Assert.That(await home.ClassifyAsync(doorLock.RootElement, CancellationToken.None), Is.EqualTo(ActionClass.Critical));
        });
    }

    private sealed class FakeClassifier : IToolServerClassifier
    {
        public string? Asked { get; private set; }

        public string Kind => ToolServerKinds.HomeAssistant;

        public ActionClass LowestClass(ToolServerTool tool) => tool.ReadOnly ? ActionClass.Read : ActionClass.EverydayEffect;

        public async ValueTask<ActionClass> ClassifyAsync(ToolServerCall call, CancellationToken cancellationToken = default)
        {
            Asked = call.Server + ":" + (await call.Ask("read_state", call.Arguments, cancellationToken)).Text;
            return call.Arguments.GetProperty("name").GetString() == "serrure" ? ActionClass.Critical : ActionClass.EverydayEffect;
        }
    }

    private sealed class FakeConnector : IToolServerConnector
    {
        private int connections;

        public IReadOnlyList<ToolServerTool> Tools { get; set; } = [];

        public ToolServerFailure? FailWith { get; set; }

        /// <summary>How many of the next calls fail with a network error.</summary>
        public int CallFailures { get; set; }

        public string? ToolError { get; set; }

        public int Connections => Volatile.Read(ref connections);

        public ToolServerEndpoint? LastEndpoint { get; private set; }

        public FakeConnection? Last { get; private set; }

        public ValueTask<IToolServerConnection> ConnectAsync(ToolServerEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref connections);
            LastEndpoint = endpoint;
            if (FailWith is { } failure)
            {
                throw new ToolServerException(failure);
            }

            Last = new FakeConnection(this);
            return ValueTask.FromResult<IToolServerConnection>(Last);
        }
    }

    private sealed class FakeConnection(FakeConnector owner) : IToolServerConnection
    {
        private readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Disposed { get; private set; }

        public string? AnnouncedName => "Fake server";

        public Task Closed => closed.Task;

        /// <summary>The process stopped or the stream closed.</summary>
        public void Close() => closed.TrySetResult();

        public ValueTask<IReadOnlyList<ToolServerTool>> ListToolsAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(owner.Tools);

        public ValueTask<ToolServerResult> CallAsync(string tool, JsonElement arguments, CancellationToken cancellationToken = default)
        {
            if (closed.Task.IsCompleted)
            {
                throw new ToolServerException(ToolServerFailure.Closed);
            }

            if (owner.CallFailures > 0)
            {
                owner.CallFailures--;
                throw new ToolServerException(ToolServerFailure.Unreachable);
            }

            return ValueTask.FromResult(owner.ToolError is { } error ? new ToolServerResult(error, IsError: true) : new ToolServerResult(tool + ": ok", IsError: false));
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
