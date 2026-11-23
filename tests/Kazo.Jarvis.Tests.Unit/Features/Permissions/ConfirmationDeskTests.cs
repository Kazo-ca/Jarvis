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

namespace Kazo.Jarvis.Tests.Unit.Features.Permissions;

/// <summary>
/// The confirmation request (ADR 0023): the condition and the window while it is open, the question said for a
/// spoken turn, the announcement for a typed one, the channels each class accepts, the wait without a timeout, the
/// interruption. Runs with the real kernel.
/// </summary>
[TestFixture]
public sealed class ConfirmationDeskTests
{
    private static readonly ActionRequester Model = new(RequesterKind.Tool, "local");
    private static readonly ActionContext Spoken = new(new TurnId(1), true);
    private static readonly ActionContext Typed = new(new TurnId(1), false);

    private TemporaryFolder folder = null!;
    private ServiceProvider provider = null!;
    private RecordingDialogs dialogs = null!;
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
        dialogs = new RecordingDialogs();
        language = new ManualLanguage { Current = AssistantLanguage.French };
        desk = new ConfirmationDesk(Bus, dialogs, State, language, NullLogger<ConfirmationDesk>.Instance);
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
            Assert.That(State.Current.Interaction, Is.EqualTo(new PendingInteraction(open.Id, InteractionKind.Confirmation)));
            Assert.That(State.Current.Look, Is.EqualTo(AssistantLook.InteractionRequired));
            Assert.That(dialogs.IsOpen(ConfirmationDesk.DialogKey), Is.True);
            Assert.That(dialogs.Opened.Single().Component, Is.EqualTo(typeof(ConfirmationWindow)));
            Assert.That(open.Summary, Is.EqualTo("Jarvis veut écrire la note « lait »."));
            Assert.That(open.Request.Detail, Is.EqualTo("""{"text":"lait"}"""), "The exact detail, nothing cut.");
        });

        desk.Click(open.Id, true);
        var outcome = await asking;

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(new ConfirmationOutcome(ConfirmationAnswer.Approved, ConfirmationChannels.Click)));
            Assert.That(State.Current.Interaction, Is.Null);
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
    public async Task A_typed_turn_only_hears_that_an_interaction_is_required()
    {
        var asking = Ask(ActionClass.Sensitive, Typed, "Jarvis veut écrire la note « lait ».");
        var open = await OpenAsync();
        await Eventually(() => Published<ConfirmationQuestionToSay>().Length == 1);

        Assert.Multiple(() =>
        {
            Assert.That(Published<ConfirmationQuestionToSay>().Single(), Is.EqualTo(
                new ConfirmationQuestionToSay(open.Id, new TurnId(1), "Votre demande a besoin de votre interaction.", false)),
                "Neither the summary nor a question: the window says the rest, and nobody listens.");
            Assert.That(open.Listens, Is.False);
        });

        // A spoken answer through a channel the request does not accept is rejected: the request stays open.
        await Bus.PublishAsync(new ConfirmationAnswerHeard(open.Id, "oui"));
        Assert.That(asking.IsCompleted, Is.False);
        desk.Click(open.Id, false);
        await asking;
    }

    [Test]
    public async Task A_request_outside_a_turn_says_nothing()
    {
        var asking = Ask(ActionClass.Sensitive, ActionContext.None);
        var open = await OpenAsync();

        Assert.That(Published<ConfirmationQuestionToSay>(), Is.Empty);
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

    // Benoit, 2026-11-21: Jarvis never decides in place of the user. No timeout refuses for them.
    [Test]
    public async Task Without_an_answer_the_request_stays_open()
    {
        var asking = Ask(ActionClass.Sensitive, Spoken);
        var open = await OpenAsync();

        await Task.Delay(300);

        Assert.Multiple(() =>
        {
            Assert.That(asking.IsCompleted, Is.False);
            Assert.That(State.Current.Interaction?.Id, Is.EqualTo(open.Id));
            Assert.That(dialogs.IsOpen(ConfirmationDesk.DialogKey), Is.True);
            Assert.That(Enum.GetNames<ConfirmationAnswer>(), Is.EquivalentTo(new[] { "Approved", "Refused", "Interrupted" }), "There is no \"timed out\" answer.");
        });
        desk.Click(open.Id, false);
        await asking;
    }

    [Test]
    public async Task The_window_closed_by_the_user_is_a_refusal()
    {
        var asking = Ask(ActionClass.Sensitive, Typed);
        await OpenAsync();

        // The user closes the window: the host says so, the desk did not ask for it.
        dialogs.Close(ConfirmationDesk.DialogKey);

        Assert.That(await asking, Is.EqualTo(new ConfirmationOutcome(ConfirmationAnswer.Refused, ConfirmationChannels.Click)));
        Assert.That(desk.Current, Is.Null);
    }

    [Test]
    public async Task Another_window_closing_answers_nothing()
    {
        var asking = Ask(ActionClass.Sensitive, Typed);
        var open = await OpenAsync();

        dialogs.Open(new DialogRequest("Settings.Window", typeof(ConfirmationWindow), "Settings"));
        dialogs.Close("Settings.Window");

        Assert.That(asking.IsCompleted, Is.False);
        desk.Click(open.Id, true);
        Assert.That((await asking).Answer, Is.EqualTo(ConfirmationAnswer.Approved));
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
            Assert.That(State.Current.Interaction, Is.Null);
            Assert.That(Published<InteractionClosed>(), Has.Length.EqualTo(1));
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
        Assert.That(State.Current.Interaction?.Kind, Is.EqualTo(InteractionKind.Confirmation));
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

        public event Action<string>? Closed;

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
            bool removed;
            lock (gate)
            {
                removed = open.Remove(key);
            }

            if (removed)
            {
                Closed?.Invoke(key);
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
