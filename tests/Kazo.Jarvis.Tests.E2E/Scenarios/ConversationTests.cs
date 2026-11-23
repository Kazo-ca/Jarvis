using System.Globalization;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The full turn on the Web host (work item stage1/13-conversation, US1.9, US1.12, US1.14, US1.15): a spoken request
/// goes through the simulated transcription, the simulated model and the simulated voice; an interruption cuts the
/// answer and listens again; a typed request gets a written answer only; the conversation window shows each turn with
/// the model that answered; the personality is changed and reset.
/// </summary>
[TestFixture]
public sealed class ConversationTests : PageTest
{
    /// <summary>What the simulated model always answers (<c>SimulatedLlmConnector</c>).</summary>
    private const string SimulatedAnswer = "Ceci est une réponse simulée";

    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Newest => Conversation.Locator("[data-conversation-turn]").First;

    private ILocator Spoken => Page.Locator("#voice-spoken");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
    }

    [TearDown]
    public async Task BackToIdle()
    {
        await CloseDialogsAsync();

        // A turn left running is interrupted (it listens again), then the listening is cancelled.
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
    public async Task A_spoken_request_is_answered_aloud_and_shown_in_the_conversation()
    {
        var spokenBefore = await CountAsync(Spoken);

        await Page.ClickAsync("#simulate-wake-word");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Page.ClickAsync("#say-phrase");

        // Transcribed, answered by the model sentence by sentence, said by the voice, then back to idle.
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Speaking", new() { Timeout = 10_000 });
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await Expect(Spoken).ToContainTextAsync("aucun modèle n'a été appelé");
        Assert.That(await CountAsync(Spoken), Is.GreaterThanOrEqualTo(spokenBefore + 2), "the answer is said in two sentences");

        await OpenConversationAsync();
        await Expect(Newest).ToHaveAttributeAsync("data-turn-input", "Voice");
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered");
        await Expect(Newest.Locator("[data-conversation-request]")).ToHaveTextAsync("What time is it?");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync(SimulatedAnswer);
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-model", "qwen3:14b");
        await Expect(Newest.Locator("[data-conversation-meta]")).ToContainTextAsync("42");
    }

    [Test]
    public async Task An_interruption_while_Jarvis_answers_listens_again_and_the_turn_shows_it()
    {
        await Page.ClickAsync("#simulate-hotkey");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Page.ClickAsync("#say-phrase");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Speaking", new() { Timeout = 10_000 });

        // "Hey Jarvis" while Jarvis speaks (US1.12): the answer stops, a new turn listens.
        await Page.ClickAsync("#simulate-wake-word");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Page.ClickAsync("#simulate-hotkey");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");

        await OpenConversationAsync();
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Interrupted");
        await Expect(Newest.Locator("[data-conversation-request]")).ToHaveTextAsync("What time is it?");
    }

    [Test]
    public async Task A_typed_request_gets_a_written_answer_only()
    {
        await OpenConversationAsync();
        var spokenBefore = await CountAsync(Spoken);

        await Conversation.Locator("[data-conversation-text]").FillAsync("Bonsoir, Jarvis.");
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");

        await Expect(Newest.Locator("[data-conversation-request]")).ToHaveTextAsync("Bonsoir, Jarvis.");
        await Expect(Newest).ToHaveAttributeAsync("data-turn-input", "Text");
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync(SimulatedAnswer);
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");

        // Jarvis speaks only when spoken to: nothing was said.
        await Page.WaitForTimeoutAsync(500);
        Assert.That(await CountAsync(Spoken), Is.EqualTo(spokenBefore));
    }

    [Test]
    public async Task A_tool_call_is_shown_in_the_turn_with_its_arguments_and_its_result()
    {
        // US2.1 (ADR 0017): the simulated model asks for the tool "calculate"; the call goes through the decision
        // point, runs, and the answer quotes its result.
        await OpenConversationAsync();

        await Conversation.Locator("[data-conversation-text]").FillAsync("Calcule 12*7+3");
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");

        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 10_000 });
        var call = Newest.Locator("[data-conversation-tool='calculate']");
        await Expect(call).ToHaveCountAsync(1);
        await Expect(call).ToHaveAttributeAsync("data-tool-outcome", "Done");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("87");

        // Collapsed by default; opened, it shows the arguments and the result.
        await Expect(call.Locator("[data-conversation-tool-result]")).Not.ToBeVisibleAsync();
        await call.Locator("summary").ClickAsync();
        await Expect(call.Locator("[data-conversation-tool-arguments]")).ToContainTextAsync("12*7+3");
        await Expect(call.Locator("[data-conversation-tool-result]")).ToHaveTextAsync("87");
    }

    [Test]
    public async Task The_clock_tool_answers_with_the_date_of_the_computer()
    {
        await OpenConversationAsync();

        await Conversation.Locator("[data-conversation-text]").FillAsync("Donne-moi l'heure exacte.");
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");

        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-tool='get_datetime']")).ToHaveAttributeAsync("data-tool-outcome", "Done");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("time_zone");
    }

    [Test]
    public async Task A_tool_that_does_not_exist_is_denied_and_the_turn_still_ends()
    {
        await OpenConversationAsync();

        await Conversation.Locator("[data-conversation-text]").FillAsync("Appelle un outil inconnu.");
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");

        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 10_000 });
        var call = Newest.Locator("[data-conversation-tool]");
        await Expect(call).ToHaveAttributeAsync("data-tool-outcome", "Denied");
        await call.Locator("summary").ClickAsync();
        await Expect(call.Locator("[data-conversation-tool-result]")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("aucun outil ne porte ce nom|no tool has this name"));
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
    }

    [Test]
    public async Task Erase_everything_empties_the_conversation_after_a_confirmation()
    {
        await OpenConversationAsync();
        await Conversation.Locator("[data-conversation-text]").FillAsync("Une demande à effacer.");
        await Conversation.Locator("[data-conversation-send]").ClickAsync();
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 10_000 });

        await Conversation.Locator("[data-conversation-erase]").ClickAsync();
        await Conversation.Locator("[data-conversation-erase-cancel]").ClickAsync();
        await Expect(Conversation.Locator("[data-conversation-turn]")).Not.ToHaveCountAsync(0);

        await Conversation.Locator("[data-conversation-erase]").ClickAsync();
        await Conversation.Locator("[data-conversation-erase-confirm]").ClickAsync();
        await Expect(Conversation.Locator("[data-conversation-turn]")).ToHaveCountAsync(0);
        await Expect(Conversation.Locator("[data-conversation-empty]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task The_personality_is_changed_then_reset_to_the_butler()
    {
        await Page.ClickAsync("#open-settings");
        await Expect(Settings).ToHaveCountAsync(1);
        await Settings.Locator("[data-settings-page='Conversation.Personality']").ClickAsync();
        var french = Settings.Locator("[data-personality='fr']");
        await Expect(french).ToHaveAttributeAsync("data-personality-custom", "false");
        await Expect(french.Locator("[data-personality-text]")).ToHaveValueAsync(new System.Text.RegularExpressions.Regex("majordome"));

        await french.Locator("[data-personality-text]").FillAsync("Tu es Jarvis, un majordome très bref.");
        await french.Locator("[data-personality-save]").ClickAsync();
        await Expect(french).ToHaveAttributeAsync("data-personality-custom", "true");
        await Expect(Settings.Locator("[data-personality='en']")).ToHaveAttributeAsync("data-personality-custom", "false");

        await french.Locator("[data-personality-reset]").ClickAsync();
        await Expect(french).ToHaveAttributeAsync("data-personality-custom", "false");
        await Expect(french.Locator("[data-personality-text]")).ToHaveValueAsync(new System.Text.RegularExpressions.Regex("du film"));
    }

    private async Task OpenConversationAsync()
    {
        await Page.ClickAsync("#open-conversation");
        await Expect(Conversation).ToHaveCountAsync(1);
        await Expect(Conversation.Locator("[data-conversation-window]")).ToBeVisibleAsync();
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
