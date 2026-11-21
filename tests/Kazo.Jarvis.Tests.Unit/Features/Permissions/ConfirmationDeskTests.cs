using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Permissions.Confirmation;
using Kazo.Jarvis.Kernel;
using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Messages.Assistant;
using Kazo.Jarvis.Kernel.Messages.Permissions;
using Kazo.Jarvis.Kernel.Messaging;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Features.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Features.Permissions;

/// <summary>
/// The confirmation request (ADR 0023): the condition and the window while it is open, the question said for a
/// spoken turn, the channels each class accepts, the timeout, the interruption. Runs with the real kernel.
/// </summary>
[TestFixture]
public sealed class ConfirmationDeskTests
{
    private static readonly ActionRequester Model = new(RequesterKind.Tool, "local");
    private static readonly ActionContext Spoken = new(new TurnId(1), true);
    private static readonly ActionContext Typed = new(new TurnId(1), false);

    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private JsonFileSettingsStore settings = null!;
    private RecordingDialogs dialogs = null!;
    private FakeTimeProvider time = null!;
    private ManualLanguage language = null!;
    private ConfirmationDesk desk = null!;
    private List<IEvent> published = null!;
    private IDisposable[] subscriptions = [];

    private IMessageBus Bus => provider.GetRequiredService<IMessageBus>();

    private IAssistantStateReader State => provider.GetRequiredService<IAssistantStateReader>();

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        provider = new ServiceCollection().AddJarvisKernel().BuildServiceProvider();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        dialogs = new RecordingDialogs();
        time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 9, 0, 0, TimeSpan.Zero));
        language = new ManualLanguage { Current = AssistantLanguage.French };
        desk = new ConfirmationDesk(Bus, dialogs, settings, State, language, time, NullLogger<ConfirmationDesk>.Instance);
        published = [];
        subscriptions =
        [
            // As the slice registration does.
            Bus.Subscribe<ConfirmationAnswerHeard>(desk.HandleAsync),
            Bus.Subscribe<IEvent>((message, _) =>
            {
                lock (published)
                {
                    published.Add(message);
                }

                return ValueTask.CompletedTask;
            }),
        ];
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }

        desk.Dispose();
        provider.Dispose();
        folder.Dispose();
    }

    [Test]
    public async Task While_a_request_is_open_the_condition_is_set_and_the_window_shows_it()
    {
        var asking = Ask(ActionClass.Sensitive, Typed, "Jarvis veut écrire la note « lait ».");
        var open = await OpenAsync();

        Assert.Multiple(() =>
        {
            Assert.That(State.Current.Confirmation, Is.EqualTo(new PendingConfirmation(open.Id, ActionClass.Sensitive)));
            Assert.That(State.Current.Look, Is.EqualTo(AssistantLook.Confirmation));
            Assert.That(dialogs.IsOpen(ConfirmationDesk.DialogKey), Is.True);
            Assert.That(dialogs.Opened.Single().Component, Is.EqualTo(typeof(ConfirmationWindow)));
            Assert.That(open.Summary, Is.EqualTo("Jarvis veut écrire la note « lait »."));
            Assert.That(open.Request.Detail, Is.EqualTo("""{"text":"lait"}"""), "The exact detail, nothing cut.");
            Assert.That(open.Deadline, Is.EqualTo(time.GetUtcNow().AddSeconds(30)), "30 seconds by default.");
        });

        desk.Click(open.Id, true);
        var outcome = await asking;

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(new ConfirmationOutcome(ConfirmationAnswer.Approved, ConfirmationChannels.Click)));
            Assert.That(State.Current.Confirmation, Is.Null);
            Assert.That(dialogs.IsOpen(ConfirmationDesk.DialogKey), Is.False);
            Assert.That(desk.Current, Is.Null);
        });
    }

    [Test]
    public async Task A_click_on_refuse_is_a_refusal()
    {
        var asking = Ask(ActionClass.Critical, Typed);
        desk.Click((await OpenAsync()).Id, false);

        Assert.That(await asking, Is.EqualTo(new ConfirmationOutcome(ConfirmationAnswer.Refused, ConfirmationChannels.Click)));
    }

    [Test]
    public async Task A_tool_without_a_summary_gets_the_general_sentence_that_names_it()
    {
        var asking = Ask(ActionClass.Sensitive, Typed, summary: null);
        var open = await OpenAsync();

        Assert.That(open.Summary, Is.EqualTo("L'outil « write_note » veut agir."));
        desk.Click(open.Id, false);
        await asking;
    }

    [Test]
    public async Task A_typed_turn_gets_the_window_only()
    {
        var asking = Ask(ActionClass.Sensitive, Typed);
        var open = await OpenAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Published<ConfirmationQuestionToSay>(), Is.Empty, "Jarvis speaks only when spoken to.");
            Assert.That(open.Listens, Is.False);
        });

        // A spoken answer through a channel the request does not accept is rejected: the request stays open.
        await Bus.PublishAsync(new ConfirmationAnswerHeard(open.Id, "oui"));
        Assert.That(asking.IsCompleted, Is.False);
        desk.Click(open.Id, false);
        await asking;
    }

    [Test]
    public async Task A_spoken_turn_hears_the_summary_and_the_question_and_may_answer_aloud()
    {
        var asking = Ask(ActionClass.Sensitive, Spoken, "Jarvis veut écrire la note « lait ».");
        var open = await OpenAsync();

        Assert.That(Published<ConfirmationQuestionToSay>(), Is.EqualTo(new[]
        {
            new ConfirmationQuestionToSay(open.Id, new TurnId(1), "Jarvis veut écrire la note « lait ». Confirmez-vous ?", true),
        }));

        await Bus.PublishAsync(new ConfirmationAnswerHeard(open.Id, " Oui, Jarvis."));

        Assert.That(await asking, Is.EqualTo(new ConfirmationOutcome(ConfirmationAnswer.Approved, ConfirmationChannels.Voice)));
    }

    [TestCase("Non.")]
    [TestCase("Oui, enfin non, attends.")]
    [TestCase("Quelle heure est-il ?")]
    public async Task Any_spoken_answer_that_is_not_an_agreement_is_a_refusal(string said)
    {
        var asking = Ask(ActionClass.Sensitive, Spoken);
        await Bus.PublishAsync(new ConfirmationAnswerHeard((await OpenAsync()).Id, said));

        Assert.That(await asking, Is.EqualTo(new ConfirmationOutcome(ConfirmationAnswer.Refused, ConfirmationChannels.Voice)));
    }

    [Test]
    public async Task Nothing_heard_leaves_the_request_open_for_a_click()
    {
        var asking = Ask(ActionClass.Sensitive, Spoken);
        var open = await OpenAsync();

        await Bus.PublishAsync(new ConfirmationAnswerHeard(open.Id, null));
        await Bus.PublishAsync(new ConfirmationAnswerHeard(open.Id, "  "));
        await Bus.PublishAsync(new ConfirmationAnswerHeard(Guid.NewGuid(), "oui"));

        Assert.That(asking.IsCompleted, Is.False);
        desk.Click(open.Id, true);
        Assert.That((await asking).Channel, Is.EqualTo(ConfirmationChannels.Click));
    }

    [Test]
    public async Task A_critical_request_says_that_voice_is_not_enough_and_does_not_listen()
    {
        var asking = Ask(ActionClass.Critical, Spoken, "Jarvis veut effacer toutes les notes.");
        var open = await OpenAsync();
        var question = Published<ConfirmationQuestionToSay>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(question.Listen, Is.False);
            Assert.That(question.Text, Does.StartWith("Jarvis veut effacer toutes les notes. ").And.Contain("la voix ne suffit pas"));
            Assert.That(open.Listens, Is.False);
        });

        // Even if a listening happened, an agreement by voice does not approve a critical action.
        await Bus.PublishAsync(new ConfirmationAnswerHeard(open.Id, "oui"));
        Assert.That(asking.IsCompleted, Is.False);
        desk.Click(open.Id, true);
        Assert.That(await asking, Is.EqualTo(new ConfirmationOutcome(ConfirmationAnswer.Approved, ConfirmationChannels.Click)));
    }

    [Test]
    public async Task With_the_microphone_muted_the_question_is_said_but_only_a_click_answers()
    {
        await Bus.PublishAsync<AssistantInput>(new MicrophoneMuteChanged(true));

        var asking = Ask(ActionClass.Sensitive, Spoken);
        var open = await OpenAsync();
        var question = Published<ConfirmationQuestionToSay>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(question.Listen, Is.False);
            Assert.That(question.Text, Does.EndWith("Approuvez ou refusez dans la fenêtre."));
        });
        desk.Click(open.Id, false);
        await asking;
    }

    [Test]
    public async Task Without_an_answer_the_request_times_out()
    {
        var asking = Ask(ActionClass.Sensitive, Spoken);
        await OpenAsync();
        await Eventually(() => Published<ConfirmationQuestionToSay>().Length == 1);

        time.Advance(TimeSpan.FromSeconds(29));
        Assert.That(asking.IsCompleted, Is.False);
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Multiple(async () =>
        {
            Assert.That(await asking.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(new ConfirmationOutcome(ConfirmationAnswer.TimedOut, null)));
            Assert.That(State.Current.Confirmation, Is.Null);
            Assert.That(dialogs.IsOpen(ConfirmationDesk.DialogKey), Is.False);
        });
    }

    [Test]
    public async Task The_timeout_is_the_one_of_the_settings()
    {
        await settings.WriteAsync(ConfirmationOptions.Section, new ConfirmationOptions { TimeoutSeconds = 10 });

        var asking = Ask(ActionClass.Sensitive, Typed);
        var open = await OpenAsync();
        Assert.That(open.Deadline, Is.EqualTo(time.GetUtcNow().AddSeconds(10)));
        await Task.Delay(50);
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.That((await asking.WaitAsync(TimeSpan.FromSeconds(5))).Answer, Is.EqualTo(ConfirmationAnswer.TimedOut));
    }

    [TestCase(9)]
    [TestCase(121)]
    public void A_timeout_outside_10_to_120_seconds_is_not_a_valid_setting(int seconds)
    {
        Assert.That(ConfirmationOptions.Section.Validate(new ConfirmationOptions { TimeoutSeconds = seconds }), Is.Not.Empty);
    }

    [Test]
    public async Task An_interruption_closes_the_request()
    {
        using var turn = new CancellationTokenSource();
        var asking = Ask(ActionClass.Sensitive, Spoken, cancellation: turn.Token);
        await OpenAsync();

        await turn.CancelAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await asking.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(new ConfirmationOutcome(ConfirmationAnswer.Interrupted, null)));
            Assert.That(State.Current.Confirmation, Is.Null);
            Assert.That(Published<ConfirmationClosed>(), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task Only_one_request_is_open_at_a_time_and_the_next_one_waits()
    {
        var first = Ask(ActionClass.Sensitive, Typed, "Première.");
        var open = await OpenAsync();
        var second = Ask(ActionClass.Critical, Typed, "Seconde.");
        await Task.Delay(50);

        Assert.That(desk.Current?.Summary, Is.EqualTo("Première."));

        desk.Click(open.Id, true);
        await first;
        await Eventually(() => desk.Current?.Summary == "Seconde.");

        // A click meant for the first request does not answer the second.
        desk.Click(open.Id, true);
        Assert.That(second.IsCompleted, Is.False);
        Assert.That(State.Current.Confirmation?.Class, Is.EqualTo(ActionClass.Critical));
        desk.Click(desk.Current!.Id, false);
        Assert.That((await second).Answer, Is.EqualTo(ConfirmationAnswer.Refused));
    }

    private Task<ConfirmationOutcome> Ask(ActionClass actionClass, ActionContext context, string? summary = "Jarvis veut agir.", CancellationToken cancellation = default) =>
        Task.Run(() => desk.AskAsync(new ConfirmationRequest(Model, "write_note", summary, """{"text":"lait"}""", actionClass, context), cancellation).AsTask());

    private async Task<OpenConfirmation> OpenAsync()
    {
        await Eventually(() => desk.Current is not null && dialogs.IsOpen(ConfirmationDesk.DialogKey));
        return desk.Current!;
    }

    private T[] Published<T>()
    {
        lock (published)
        {
            return [.. published.OfType<T>()];
        }
    }

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.That(DateTime.UtcNow, Is.LessThan(deadline), "The condition did not become true within 5 seconds.");
            await Task.Delay(10);
        }
    }

    private sealed class RecordingDialogs : IDialogService
    {
        private readonly Lock gate = new();
        private readonly HashSet<string> open = [];

        public List<DialogRequest> Opened { get; } = [];

        public void Open(DialogRequest request)
        {
            lock (gate)
            {
                Opened.Add(request);
                open.Add(request.Key);
            }
        }

        public void Close(string key)
        {
            lock (gate)
            {
                open.Remove(key);
            }
        }

        public bool IsOpen(string key)
        {
            lock (gate)
            {
                return open.Contains(key);
            }
        }
    }
}
