using System.Text.Json;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Adapters.Storage;
using Kazo.Jarvis.Features.Permissions.Decision;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Kernel.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Permissions;

/// <summary>
/// The decision point (ADR 0009 and ADR 0017, section 2): the model proposes, this code decides; denied by default
/// at every step; every decision is logged. Runs with the real settings file and the real SQLite store.
/// </summary>
[TestFixture]
public sealed class PermissionDecisionPointTests
{
    private static readonly ActionRequester Model = new(RequesterKind.Tool, "local");

    private TemporaryFolder folder = null!;
    private JsonFileSettingsStore settings = null!;
    private SqliteDocumentStore store = null!;
    private ToolRegistry registry = null!;
    private PermissionDecisionPoint point = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        store = new SqliteDocumentStore(folder.Paths, TimeProvider.System);
        registry = new ToolRegistry([], settings);
        point = new PermissionDecisionPoint(registry, store, TimeProvider.System, NullLogger<PermissionDecisionPoint>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        registry.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        folder.Dispose();
    }

    private static JsonElement Arguments(string json = "{}") => JsonDocument.Parse(json).RootElement.Clone();

    private ValueTask<PermissionDecision> AskAsync(string tool, string json = "{}") =>
        point.DecideAsync(new ActionRequest(Model, tool, Arguments(json)));

    private async Task<DecisionRecord[]> LogAsync() =>
        [.. (await store.Collection<DecisionRecord>(PermissionDecisionPoint.CollectionName)
            .ReadPeriodAsync(DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow.AddDays(1))).Select(document => document.Content)];

    [TestCase(ActionClass.Read)]
    [TestCase(ActionClass.EverydayEffect)]
    public async Task A_read_and_an_everyday_effect_are_allowed_without_a_question(ActionClass actionClass)
    {
        registry.Register(new FakeTool("tool") { DeclaredClass = actionClass });

        var decision = await AskAsync("tool");

        Assert.That(decision, Is.EqualTo(PermissionDecision.Allow(actionClass)));
    }

    [TestCase(ActionClass.Sensitive)]
    [TestCase(ActionClass.Critical)]
    public async Task What_needs_a_confirmation_is_denied_while_nobody_can_be_asked(ActionClass actionClass)
    {
        // The confirmation request arrives with work item 4: until then, denied by default (ADR 0009, principle 3).
        registry.Register(new FakeTool("write_file") { DeclaredClass = actionClass });

        var decision = await AskAsync("write_file");

        Assert.That(decision, Is.EqualTo(PermissionDecision.Deny(DenialCause.ConfirmationUnavailable, actionClass)));
    }

    [Test]
    public async Task A_forbidden_call_is_denied_without_a_question()
    {
        registry.Register(new FakeTool("read_file")
        {
            Classifier = arguments => arguments.GetProperty("path").GetString()!.StartsWith("C:\\allowed", StringComparison.Ordinal) ? ActionClass.Read : ActionClass.Forbidden,
        });

        Assert.Multiple(async () =>
        {
            Assert.That(await AskAsync("read_file", """{"path":"C:\\allowed\\notes.txt"}"""), Is.EqualTo(PermissionDecision.Allow(ActionClass.Read)), "The class is decided per call, from its arguments.");
            Assert.That(await AskAsync("read_file", """{"path":"C:\\Users\\benoit\\.claude\\sessions\\a.key"}"""), Is.EqualTo(PermissionDecision.Deny(DenialCause.Forbidden, ActionClass.Forbidden)));
        });
    }

    [Test]
    public async Task An_unknown_tool_and_a_disabled_tool_are_denied()
    {
        registry.Register(new FakeTool("web_search") { EnabledByDefault = false });

        Assert.Multiple(async () =>
        {
            Assert.That(await AskAsync("open_the_pod_bay_doors"), Is.EqualTo(PermissionDecision.Deny(DenialCause.UnknownTool)));
            Assert.That(await AskAsync("web_search"), Is.EqualTo(PermissionDecision.Deny(DenialCause.ToolDisabled)));
        });

        await settings.WriteAsync(ToolActivation.Section, new ToolActivation().With("web_search", true));
        Assert.That((await AskAsync("web_search")).Allowed, Is.True, "Enabled by the user.");
    }

    [Test]
    public async Task Arguments_that_cannot_be_read_are_a_denial()
    {
        registry.Register(new FakeTool("tool"));

        Assert.Multiple(async () =>
        {
            Assert.That(await point.DecideAsync(new ActionRequest(Model, "tool", null)), Is.EqualTo(PermissionDecision.Deny(DenialCause.UnreadableArguments)));
            Assert.That(await AskAsync("tool", "[1,2]"), Is.EqualTo(PermissionDecision.Deny(DenialCause.UnreadableArguments)));
        });
    }

    [Test]
    public async Task A_classifier_that_fails_is_a_denial()
    {
        registry.Register(new FakeTool("tool") { Classifier = _ => throw new InvalidOperationException("boom") });
        registry.Register(new FakeTool("strange") { Classifier = _ => (ActionClass)42 });

        Assert.Multiple(async () =>
        {
            Assert.That(await AskAsync("tool"), Is.EqualTo(PermissionDecision.Deny(DenialCause.ClassifierFailed)));
            Assert.That(await AskAsync("strange"), Is.EqualTo(PermissionDecision.Deny(DenialCause.ClassifierFailed)), "A class that does not exist allows nothing.");
        });
    }

    [Test]
    public async Task A_classifier_cannot_go_below_the_class_the_tool_declares()
    {
        registry.Register(new FakeTool("write_file") { DeclaredClass = ActionClass.Sensitive, Classifier = _ => ActionClass.Read });

        var decision = await AskAsync("write_file");

        Assert.That(decision, Is.EqualTo(PermissionDecision.Deny(DenialCause.ConfirmationUnavailable, ActionClass.Sensitive)));
    }

    [Test]
    public async Task An_interruption_is_a_denial_for_the_log()
    {
        registry.Register(new FakeTool("tool"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        Assert.That(async () => await point.DecideAsync(new ActionRequest(Model, "tool", Arguments()), cancelled.Token), Throws.InstanceOf<OperationCanceledException>());

        var record = (await LogAsync()).Single();
        Assert.Multiple(() =>
        {
            Assert.That(record.Allowed, Is.False);
            Assert.That(record.Cause, Is.EqualTo(DenialCause.Interrupted));
        });
    }

    [Test]
    public async Task Every_decision_is_logged_with_its_arguments_cut()
    {
        registry.Register(new FakeTool("calculate"));
        var longText = new string('7', 2000);

        await AskAsync("calculate", $$"""{"expression":"{{longText}}"}""");
        await AskAsync("unknown");

        var log = await LogAsync();
        Assert.Multiple(() =>
        {
            Assert.That(log, Has.Length.EqualTo(2));
            Assert.That(log[0].Tool, Is.EqualTo("calculate"));
            Assert.That(log[0].Requester, Is.EqualTo("local"));
            Assert.That(log[0].RequesterKind, Is.EqualTo(RequesterKind.Tool));
            Assert.That(log[0].Class, Is.EqualTo(ActionClass.Read));
            Assert.That(log[0].Allowed, Is.True);
            Assert.That(log[0].Arguments, Has.Length.EqualTo(PermissionDecisionPoint.MostArgumentCharacters + 1), "Cut, with an ellipsis.");
            Assert.That(log[1].Allowed, Is.False);
            Assert.That(log[1].Cause, Is.EqualTo(DenialCause.UnknownTool));
            Assert.That(log[1].Class, Is.Null);
        });
    }
}
