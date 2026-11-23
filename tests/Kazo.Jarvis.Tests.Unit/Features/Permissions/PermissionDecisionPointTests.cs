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

    /// <summary>Rebuilds the decision point with a confirmation that always gives <paramref name="outcome"/>; returns what it was asked.</summary>
    private List<ConfirmationRequest> WithConfirmation(ConfirmationOutcome outcome)
    {
        var confirmation = new FakeConfirmation(_ => outcome);
        point = new PermissionDecisionPoint(
            registry,
            store,
            TimeProvider.System,
            NullLogger<PermissionDecisionPoint>.Instance,
            new Kazo.Jarvis.Tests.Unit.Features.Settings.ManualLanguage { Current = Kazo.Jarvis.Kernel.Localization.AssistantLanguage.French },
            confirmation);
        return confirmation.Asked;
    }

    [Test]
    public async Task A_read_of_a_session_is_allowed_without_a_question_and_logged()
    {
        var asked = WithConfirmation(new ConfirmationOutcome(ConfirmationAnswer.Approved, ConfirmationChannels.Click));

        var decision = await point.DecideAsync(SessionRequest(ActionClass.Read));

        Assert.Multiple(() =>
        {
            Assert.That(decision, Is.EqualTo(PermissionDecision.Allow(ActionClass.Read)));
            Assert.That(asked, Is.Empty);
        });
    }

    [Test]
    public async Task A_write_of_a_session_asks_the_human_with_the_tool_and_the_exact_input()
    {
        var asked = WithConfirmation(new ConfirmationOutcome(ConfirmationAnswer.Approved, ConfirmationChannels.Voice));

        var decision = await point.DecideAsync(SessionRequest(ActionClass.Sensitive, "Écrire notes.txt"));

        Assert.Multiple(() =>
        {
            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.Channel, Is.EqualTo(ConfirmationChannels.Voice));
            Assert.That(asked, Has.Count.EqualTo(1));
            Assert.That(asked[0].Requester, Is.EqualTo(new ActionRequester(RequesterKind.Session, "kazo-bar")));
            Assert.That(asked[0].Tool, Is.EqualTo("Write"), "The tool of Claude Code, which is in no registry of Jarvis.");
            Assert.That(asked[0].Summary, Is.EqualTo("Écrire notes.txt"));
            Assert.That(asked[0].Detail, Does.Contain("notes.txt"));
            Assert.That(asked[0].Class, Is.EqualTo(ActionClass.Sensitive));
        });
    }

    [Test]
    public async Task A_session_refused_by_the_human_is_denied()
    {
        WithConfirmation(new ConfirmationOutcome(ConfirmationAnswer.Refused, ConfirmationChannels.Click));

        var decision = await point.DecideAsync(SessionRequest(ActionClass.Sensitive));

        Assert.That(decision, Is.EqualTo(PermissionDecision.Deny(DenialCause.Refused, ActionClass.Sensitive) with { Channel = ConfirmationChannels.Click }));
    }

    [Test]
    public async Task A_forbidden_action_of_a_session_is_denied_without_a_question()
    {
        var asked = WithConfirmation(new ConfirmationOutcome(ConfirmationAnswer.Approved, ConfirmationChannels.Click));

        var decision = await point.DecideAsync(SessionRequest(ActionClass.Forbidden));

        Assert.Multiple(() =>
        {
            Assert.That(decision, Is.EqualTo(PermissionDecision.Deny(DenialCause.Forbidden, ActionClass.Forbidden)));
            Assert.That(asked, Is.Empty);
        });
    }

    [Test]
    public async Task A_session_request_without_a_class_or_with_unreadable_input_is_denied()
    {
        WithConfirmation(new ConfirmationOutcome(ConfirmationAnswer.Approved, ConfirmationChannels.Click));
        var session = new ActionRequester(RequesterKind.Session, "kazo-bar");

        var unclassified = await point.DecideAsync(new ActionRequest(session, "Write", Arguments()));
        var unreadable = await point.DecideAsync(new ActionRequest(session, "Write", null) { Session = new SessionAction(ActionClass.Read) });
        var unknownClass = await point.DecideAsync(new ActionRequest(session, "Write", Arguments()) { Session = new SessionAction((ActionClass)42) });
        var asTool = await point.DecideAsync(new ActionRequest(Model, "Write", Arguments()) { Session = new SessionAction(ActionClass.Read) });

        Assert.Multiple(() =>
        {
            Assert.That(unclassified.Cause, Is.EqualTo(DenialCause.UnknownTool));
            Assert.That(unreadable.Cause, Is.EqualTo(DenialCause.UnreadableArguments));
            Assert.That(unknownClass.Cause, Is.EqualTo(DenialCause.ClassifierFailed));
            Assert.That(asTool.Cause, Is.EqualTo(DenialCause.UnknownTool), "A class given by a model's tool call is not listened to.");
        });
    }

    private static ActionRequest SessionRequest(ActionClass actionClass, string? summary = null) =>
        new(new ActionRequester(RequesterKind.Session, "kazo-bar"), "Write", Arguments("""{"file_path":"C:\\src\\kazo-bar\\notes.txt","content":"ok"}"""))
        {
            Session = new SessionAction(actionClass, summary),
        };

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
        // A host without the confirmation slice: denied by default (ADR 0009, principle 3).
        registry.Register(new FakeTool("write_file") { DeclaredClass = actionClass });

        var decision = await AskAsync("write_file");

        Assert.That(decision, Is.EqualTo(PermissionDecision.Deny(DenialCause.ConfirmationUnavailable, actionClass)));
    }

    [TestCase(ActionClass.Sensitive, ConfirmationChannels.Click)]
    [TestCase(ActionClass.Sensitive, ConfirmationChannels.Voice)]
    [TestCase(ActionClass.Critical, ConfirmationChannels.Click)]
    public async Task An_agreement_through_an_accepted_channel_allows_the_call_and_the_channel_is_logged(ActionClass actionClass, string channel)
    {
        var asked = WithConfirmation(new ConfirmationOutcome(ConfirmationAnswer.Approved, channel));
        registry.Register(new FakeTool("write_note")
        {
            DeclaredClass = actionClass,
            Summary = (arguments, language) => $"Note « {arguments.GetProperty("text").GetString()} » ({language.Code}).",
        });
        var context = new ActionContext(new Kazo.Jarvis.Kernel.Assistant.TurnId(7), true);

        var decision = await point.DecideAsync(new ActionRequest(Model, "write_note", Arguments("""{"text":"lait"}""")) { Context = context });

        Assert.Multiple(async () =>
        {
            Assert.That(decision, Is.EqualTo(PermissionDecision.Allow(actionClass) with { Channel = channel }));
            Assert.That(asked.Single(), Is.EqualTo(new ConfirmationRequest(Model, "write_note", "Note « lait » (fr).", """{"text":"lait"}""", actionClass, context)),
                "The summary is written by the tool, in the language of the assistant; the detail is exact; the turn follows.");
            var record = (await LogAsync()).Single();
            Assert.That((record.Allowed, record.Class, record.Cause, record.Channel), Is.EqualTo((true, (ActionClass?)actionClass, (DenialCause?)null, channel)));
        });
    }

    [Test]
    public async Task A_critical_call_is_never_approved_by_voice()
    {
        // ADR 0023, section 4: whatever the confirmation slice answers, the decision point checks the channel.
        WithConfirmation(new ConfirmationOutcome(ConfirmationAnswer.Approved, ConfirmationChannels.Voice));
        registry.Register(new FakeTool("erase_notes") { DeclaredClass = ActionClass.Critical });

        var decision = await AskAsync("erase_notes");

        Assert.Multiple(async () =>
        {
            Assert.That(decision, Is.EqualTo(PermissionDecision.Deny(DenialCause.Refused, ActionClass.Critical) with { Channel = ConfirmationChannels.Voice }));
            Assert.That((await LogAsync()).Single().Channel, Is.EqualTo(ConfirmationChannels.Voice), "The rejected agreement is logged with its channel.");
        });
    }

    [Test]
    public async Task An_agreement_without_a_channel_is_not_an_agreement()
    {
        WithConfirmation(new ConfirmationOutcome(ConfirmationAnswer.Approved, null));
        registry.Register(new FakeTool("write_note") { DeclaredClass = ActionClass.Sensitive });

        Assert.That((await AskAsync("write_note")).Allowed, Is.False);
    }

    [TestCase(ConfirmationAnswer.Refused, ConfirmationChannels.Click, DenialCause.Refused)]
    [TestCase(ConfirmationAnswer.Refused, ConfirmationChannels.Voice, DenialCause.Refused)]
    [TestCase(ConfirmationAnswer.Interrupted, null, DenialCause.Interrupted)]
    public async Task Anything_but_an_agreement_is_a_denial_with_its_cause(ConfirmationAnswer answer, string? channel, DenialCause cause)
    {
        WithConfirmation(new ConfirmationOutcome(answer, channel));
        registry.Register(new FakeTool("write_note") { DeclaredClass = ActionClass.Sensitive });

        var decision = await AskAsync("write_note");

        Assert.Multiple(async () =>
        {
            Assert.That(decision, Is.EqualTo(PermissionDecision.Deny(cause, ActionClass.Sensitive) with { Channel = channel }));
            var record = (await LogAsync()).Single();
            Assert.That((record.Allowed, record.Cause, record.Channel), Is.EqualTo((false, (DenialCause?)cause, channel)));
        });
    }

    [Test]
    public async Task A_confirmation_that_fails_is_a_denial_and_a_summary_that_fails_does_not_stop_the_request()
    {
        var asked = WithConfirmation(new ConfirmationOutcome(ConfirmationAnswer.Approved, ConfirmationChannels.Click));
        registry.Register(new FakeTool("write_note") { DeclaredClass = ActionClass.Sensitive, Summary = (_, _) => throw new InvalidOperationException("no summary") });

        Assert.That((await AskAsync("write_note")).Allowed, Is.True);
        Assert.That(asked.Single().Summary, Is.Null, "The confirmation slice then shows its general sentence.");

        point = new PermissionDecisionPoint(registry, store, TimeProvider.System, NullLogger<PermissionDecisionPoint>.Instance, null, new FakeConfirmation(_ => throw new InvalidOperationException("no window")));
        Assert.That(await AskAsync("write_note"), Is.EqualTo(PermissionDecision.Deny(DenialCause.ConfirmationUnavailable, ActionClass.Sensitive)));
    }

    [Test]
    public async Task A_read_does_not_ask_for_a_confirmation()
    {
        var asked = WithConfirmation(new ConfirmationOutcome(ConfirmationAnswer.Refused, ConfirmationChannels.Click));
        registry.Register(new FakeTool("get_datetime"));

        Assert.That((await AskAsync("get_datetime")).Allowed, Is.True);
        Assert.That(asked, Is.Empty);
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
    public async Task A_class_found_after_a_question_to_a_server_decides()
    {
        registry.Register(new FakeTool("turn_on")
        {
            DeclaredClass = ActionClass.EverydayEffect,
            AsyncClassifier = async (arguments, cancellation) =>
            {
                await Task.Delay(5, cancellation);
                return arguments.GetProperty("name").GetString() == "serrure" ? ActionClass.Critical : ActionClass.EverydayEffect;
            },
        });
        registry.Register(new FakeTool("lost") { AsyncClassifier = (_, _) => throw new InvalidOperationException("no answer") });

        Assert.Multiple(async () =>
        {
            Assert.That(await AskAsync("turn_on", """{"name":"lampe"}"""), Is.EqualTo(PermissionDecision.Allow(ActionClass.EverydayEffect)));
            Assert.That(await AskAsync("turn_on", """{"name":"serrure"}"""), Is.EqualTo(PermissionDecision.Deny(DenialCause.ConfirmationUnavailable, ActionClass.Critical)));
            Assert.That(await AskAsync("lost"), Is.EqualTo(PermissionDecision.Deny(DenialCause.ClassifierFailed)), "Without a class, nothing is allowed.");
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

/// <summary>A confirmation that answers what the test decides, and keeps what it was asked.</summary>
internal sealed class FakeConfirmation(Func<ConfirmationRequest, ConfirmationOutcome> answer) : IConfirmation
{
    public List<ConfirmationRequest> Asked { get; } = [];

    public ValueTask<ConfirmationOutcome> AskAsync(ConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        lock (Asked)
        {
            Asked.Add(request);
        }

        return ValueTask.FromResult(answer(request));
    }
}
