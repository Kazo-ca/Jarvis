using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The confirmation request on the Web host (work item stage2/4-confirmation, ADR 0023): the two simulated tools
/// <c>write_note</c> ("Sensitive") and <c>erase_notes</c> ("Critical") only run after a human answer. After a typed
/// request Jarvis only says that an interaction is required; after a spoken one he says the question and may be
/// answered aloud; anything but an agreement is a refusal; a request waits as long as it takes; the permission log
/// keeps the channel of the answer.
/// </summary>
[TestFixture]
public sealed class ConfirmationTests : PageTest
{
    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Window => Page.Locator("[data-dialog-key='Permissions.Confirmation']");

    private ILocator Newest => Conversation.Locator("[data-conversation-turn]").First;

    private ILocator Pending => Page.Locator("#confirmation");

    private ILocator Notes => Page.Locator("#notes");

    private ILocator Spoken => Page.Locator("#voice-spoken");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await Expect(Pending).ToHaveAttributeAsync("data-pending", "false");
    }

    [TearDown]
    public async Task BackToIdle()
    {
        // Closing the confirmation window refuses its request.
        await CloseDialogsAsync();
        var phase = await Root.GetAttributeAsync("data-phase");
        if (phase is "Thinking" or "Speaking")
        {
            await Page.ClickAsync("#simulate-hotkey");
            await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        }

        if (await Root.GetAttributeAsync("data-phase") == "Listening")
        {
            await Page.ClickAsync("#simulate-hotkey");
        }

        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
    }

    [Test]
    public async Task A_typed_request_for_a_sensitive_tool_waits_for_a_click_and_runs_once_approved()
    {
        var note = "acheter du lait " + Guid.NewGuid().ToString("N")[..6];
        var spokenBefore = await CountAsync(Spoken);
        await TypeAsync("Écris une note " + note);

        // The condition is set, the orb shows it, and the phase did not change: the tool loop waits.
        await Expect(Window).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Expect(Pending).ToHaveAttributeAsync("data-pending", "true");
        await Expect(Pending).ToHaveAttributeAsync("data-kind", "Confirmation");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Thinking");
        await Expect(Page.Locator(".orb-frame .orb")).ToHaveAttributeAsync("data-look", "InteractionRequired");

        // Who asks, the summary written by code, the exact detail, the class, the time left.
        await Expect(Window.Locator("[data-confirmation-summary]")).ToContainTextAsync(note);
        await Expect(Window.Locator("[data-confirmation-tool]")).ToHaveTextAsync("write_note");
        await Expect(Window.Locator("[data-confirmation-detail]")).ToHaveTextAsync("{\"text\":\"" + note + "\"}");
        await Expect(Window.Locator("[data-confirmation-window]")).ToHaveAttributeAsync("data-confirmation-class", "Sensitive");
        await Expect(Window.Locator("[data-confirmation-channel]")).ToHaveAttributeAsync("data-confirmation-channel", "click");
        await Expect(Window.Locator("[data-confirmation-waits]")).ToBeVisibleAsync();

        // A typed turn: Jarvis only says that an interaction is required, and waits. No timeout decides for the user.
        await Expect(Spoken).ToHaveTextAsync(new Regex("a besoin de votre interaction|needs your interaction"), new() { Timeout = 10_000 });
        Assert.That(await CountAsync(Spoken), Is.EqualTo(spokenBefore + 1));
        await Page.WaitForTimeoutAsync(1_500);
        await Expect(Window).ToHaveCountAsync(1);
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Thinking");
        await Expect(Notes).Not.ToHaveTextAsync(note);

        await Window.Locator("[data-confirmation-approve]").ClickAsync();

        await Expect(Window).ToHaveCountAsync(0);
        await Expect(Pending).ToHaveAttributeAsync("data-pending", "false");
        await Expect(Notes).ToHaveTextAsync(note);
        await Expect(Newest.Locator("[data-conversation-tool='write_note']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 10_000 });

        // The answer of a typed turn is written only: nothing more was said.
        Assert.That(await CountAsync(Spoken), Is.EqualTo(spokenBefore + 1));

        await ExpectLoggedAsync("write_note", allowed: true, cause: string.Empty, channel: "click");
    }

    [Test]
    public async Task A_refused_critical_tool_does_not_run()
    {
        var erasedBefore = await Notes.GetAttributeAsync("data-erased");
        await TypeAsync("Efface les notes");

        await Expect(Window).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Expect(Window.Locator("[data-confirmation-window]")).ToHaveAttributeAsync("data-confirmation-class", "Critical");
        await Window.Locator("[data-confirmation-refuse]").ClickAsync();

        await Expect(Window).ToHaveCountAsync(0);
        var call = Newest.Locator("[data-conversation-tool='erase_notes']");
        await Expect(call).ToHaveAttributeAsync("data-tool-outcome", "Denied", new() { Timeout = 10_000 });
        await call.Locator("summary").ClickAsync();
        await Expect(call.Locator("[data-conversation-tool-result]")).ToHaveTextAsync(new Regex("l'utilisateur a refusé|the user refused"));
        await Expect(Notes).ToHaveAttributeAsync("data-erased", erasedBefore ?? "0");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 10_000 });

        await ExpectLoggedAsync("erase_notes", allowed: false, cause: "Refused", channel: "click");
    }

    [Test]
    public async Task A_spoken_request_hears_the_question_and_a_spoken_yes_approves()
    {
        var note = "appeler le garage " + Guid.NewGuid().ToString("N")[..6];
        var yes = await AskAloudAsync("Écris une note " + note, note);

        // The single listening that follows the question hears the answer; code recognizes it.
        await SayAsync(yes);

        await Expect(Window).ToHaveCountAsync(0, new() { Timeout = 10_000 });
        await Expect(Notes).ToHaveTextAsync(note);
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });

        // The answer to the question never became a request of the conversation.
        await Page.ClickAsync("#open-conversation");
        await Expect(Newest.Locator("[data-conversation-request]")).ToHaveTextAsync(new Regex("^Écris une note"));
        await Expect(Newest.Locator("[data-conversation-tool='write_note']")).ToHaveAttributeAsync("data-tool-outcome", "Done");
        await ExpectLoggedAsync("write_note", allowed: true, cause: string.Empty, channel: "voice");
    }

    [Test]
    public async Task A_spoken_answer_that_is_not_an_agreement_is_a_refusal()
    {
        var note = "ne pas écrire " + Guid.NewGuid().ToString("N")[..6];
        var yes = await AskAloudAsync("Écris une note " + note, note);

        // The word of agreement is in the sentence, but the sentence is not in the list.
        await SayAsync(yes + ", enfin non, attends.");

        await Expect(Window).ToHaveCountAsync(0, new() { Timeout = 10_000 });
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await Expect(Notes).Not.ToHaveTextAsync(note);
        await ExpectLoggedAsync("write_note", allowed: false, cause: "Refused", channel: "voice");
    }

    [Test]
    public async Task A_spoken_request_for_a_critical_tool_says_that_voice_is_not_enough()
    {
        var erasedBefore = int.Parse(await Notes.GetAttributeAsync("data-erased") ?? "0", CultureInfo.InvariantCulture);
        await Page.FillAsync("#phrase-text", "Efface les notes");
        await Page.ClickAsync("#simulate-wake-word");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Page.ClickAsync("#say-text");

        await Expect(Window).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Expect(Spoken).ToHaveTextAsync(new Regex("la voix ne suffit pas|voice is not enough"), new() { Timeout = 10_000 });
        await Expect(Window.Locator("[data-confirmation-channel]")).ToHaveAttributeAsync("data-confirmation-channel", "click");

        // The question ends and nobody listens: only the click approves.
        await Expect(Pending).ToHaveAttributeAsync("data-question-said", "true", new() { Timeout = 10_000 });
        await Window.Locator("[data-confirmation-approve]").ClickAsync();

        await Expect(Notes).ToHaveAttributeAsync("data-erased", (erasedBefore + 1).ToString(CultureInfo.InvariantCulture), new() { Timeout = 10_000 });
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await ExpectLoggedAsync("erase_notes", allowed: true, cause: string.Empty, channel: "click");
    }

    [Test]
    public async Task An_activation_during_a_request_interrupts_the_turn_and_closes_the_request()
    {
        var note = "interrompue " + Guid.NewGuid().ToString("N")[..6];
        await TypeAsync("Écris une note " + note);
        await Expect(Window).ToHaveCountAsync(1, new() { Timeout = 10_000 });

        // The windows cover the page of the host: the hotkey is "pressed" without the pointer.
        await Page.Locator("#simulate-hotkey").DispatchEventAsync("click");

        await Expect(Window).ToHaveCountAsync(0);
        await Expect(Pending).ToHaveAttributeAsync("data-pending", "false");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Expect(Notes).Not.ToHaveTextAsync(note);
        await Page.Locator("#simulate-hotkey").DispatchEventAsync("click");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
        await ExpectLoggedAsync("write_note", allowed: false, cause: "Interrupted", channel: string.Empty);
    }

    /// <summary>
    /// A spoken request for the note tool, up to the end of the question. Returns the word of agreement of the
    /// language Jarvis asked in.
    /// </summary>
    private async Task<string> AskAloudAsync(string request, string note)
    {
        await Page.FillAsync("#phrase-text", request);
        await Page.ClickAsync("#simulate-wake-word");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Page.ClickAsync("#say-text");

        await Expect(Window).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Expect(Window.Locator("[data-confirmation-channel]")).ToHaveAttributeAsync("data-confirmation-channel", "voice");

        // Jarvis says the summary and the question, in the voice of the turn, without leaving the thinking phase.
        await Expect(Spoken).ToHaveTextAsync(new Regex(Regex.Escape(note) + ".*(Confirmez-vous \\?|Do you confirm\\?)"), new() { Timeout = 10_000 });
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Thinking");
        await Expect(Pending).ToHaveAttributeAsync("data-question-said", "true", new() { Timeout = 10_000 });
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Thinking");
        return (await Spoken.TextContentAsync() ?? string.Empty).Contains("Confirmez-vous", StringComparison.Ordinal) ? "Oui" : "Yes";
    }

    /// <summary>Says <paramref name="phrase"/> while a window covers the page of the host.</summary>
    private async Task SayAsync(string phrase)
    {
        await Page.Locator("#phrase-text").FillAsync(phrase, new() { Force = true });
        await Page.Locator("#say-text").DispatchEventAsync("click");
    }

    private async Task TypeAsync(string request)
    {
        await Page.ClickAsync("#open-conversation");
        await Expect(Conversation.Locator("[data-conversation-window]")).ToBeVisibleAsync();
        await Conversation.Locator("[data-conversation-text]").FillAsync(request);
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");
    }

    /// <summary>The newest decision of the permission log is the one described.</summary>
    private async Task ExpectLoggedAsync(string tool, bool allowed, string cause, string channel)
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-settings");
        await Settings.Locator("[data-settings-page='Permissions.Decision']").ClickAsync();
        var newest = Settings.Locator("[data-decision-log] > li").First;
        await Expect(newest).ToHaveAttributeAsync("data-decision-tool", tool);
        await Expect(newest).ToHaveAttributeAsync("data-decision-allowed", allowed ? "true" : "false");
        await Expect(newest).ToHaveAttributeAsync("data-decision-cause", cause);
        await Expect(newest).ToHaveAttributeAsync("data-decision-channel", channel);
    }

    private async Task CloseDialogsAsync()
    {
        // The dialogs live in the host, not in the page: a window left open would cover the buttons of the next test.
        if (await Settings.CountAsync() == 1)
        {
            // The settings window remembers its page; the next test starts on the first one.
            await Settings.Locator("[data-settings-page='Settings.Language']").ClickAsync();
        }

        var dialogs = Page.Locator(".dialog-layer");
        for (var open = await dialogs.CountAsync(); open > 0; open--)
        {
            await dialogs.Last.Locator(".dialog-close").ClickAsync();
            await Expect(dialogs).ToHaveCountAsync(open - 1);
        }
    }

    private static async Task<int> CountAsync(ILocator output) =>
        int.Parse(await output.GetAttributeAsync("data-count") ?? "0", CultureInfo.InvariantCulture);
}
