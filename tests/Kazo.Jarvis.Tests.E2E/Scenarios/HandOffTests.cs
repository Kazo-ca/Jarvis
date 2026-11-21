using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The hand-off on the Web host (work item stage2/9-hand-off, ADR 0020, US2.5 to US2.7): nothing is handed over
/// without a target, the page says what leaves the machine, a chosen cloud provider answers when the local model
/// calls its tool or when the request starts with "Ask Claude", the conversation says who answered and why, the
/// memory goes only when its box is ticked, and a target that fails is a cause told, never a fault. The models
/// are simulated: the local one hands over on « question difficile », the cloud one says back what it received.
/// </summary>
[TestFixture]
public sealed class HandOffTests : PageTest
{
    private const string Tool = "hand_off_to_claude";

    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Newest => Conversation.Locator("[data-conversation-turn]").First;

    private ILocator Claude => Settings.Locator("[data-provider-id='anthropic']");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await ResetAsync();
    }

    [TearDown]
    public async Task Restore()
    {
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await ResetAsync();
        await CloseDialogsAsync();
    }

    [Test]
    public async Task Without_a_target_nothing_is_handed_over_and_the_page_says_what_would_leave()
    {
        await OpenSettingsAsync("Llm.HandOff");
        await Expect(Settings.Locator(".settings-frame-title")).ToHaveTextAsync("Hand-off");
        await Expect(Settings.Locator("[data-handoff-questions]")).ToHaveValueAsync(string.Empty);
        await Expect(Settings.Locator("[data-handoff-no-provider]")).ToBeVisibleAsync();
        await Expect(Settings.Locator("[data-handoff-leaves] li")).ToHaveCountAsync(4);
        await Expect(Settings.Locator("[data-handoff-leaves]")).ToContainTextAsync("Your request and the recent conversation, word for word");
        await Expect(Settings.Locator("[data-handoff-leaves]")).ToContainTextAsync("Never: a secret");
        await Expect(Settings.Locator("[data-handoff-cost]")).ToContainTextAsync("billed by the provider");
        await Expect(Settings.Locator("[data-handoff-cost]")).ToContainTextAsync("A monthly cap bounds these calls");
        await Expect(Settings.Locator("[data-handoff-memory]")).Not.ToBeCheckedAsync();
        await Expect(Settings.Locator("[data-handoff-phrases] li")).ToHaveTextAsync(new[] { "Ask Claude…", "Ask Claude Code…" });

        // The local model is not offered a tool it cannot use.
        await OpenSettingsAsync("Tools.Catalog");
        await Expect(Settings.Locator("[data-tools-list]")).ToBeVisibleAsync();
        await Expect(Settings.Locator($"[data-tool='{Tool}']")).ToHaveCountAsync(0);

        await SayAsync("Une question difficile sur Kant");
        await Expect(Newest.Locator("[data-conversation-tool]")).ToHaveCountAsync(0);
        await Expect(Newest.Locator("[data-conversation-handoff]")).ToHaveCountAsync(0);
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-model", "qwen3:14b");

        // Forced without a target: Jarvis says so and nobody answers in its place.
        await SayAsync("Ask Claude what a monad is");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("The hand-off did not happen: no target is set for questions.");
        await Expect(Newest.Locator("[data-conversation-handoff]")).ToHaveAttributeAsync("data-handoff-failure", "NoQuestionsTarget");
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-model", string.Empty);
    }

    [Test]
    public async Task A_chosen_cloud_provider_answers_by_the_tool_and_by_ask_claude()
    {
        await AddClaudeAsync("sk-ant-simulated");
        await ChooseAsync("anthropic");
        await Expect(Settings.Locator("[data-handoff-target='anthropic']")).ToContainTextAsync("I am asking Claude");
        await Expect(Settings.Locator("[data-handoff-target='anthropic']")).ToContainTextAsync("api.anthropic.com");

        // The tool is there, on, and says what leaves the machine.
        await OpenSettingsAsync("Tools.Catalog");
        var tool = Settings.Locator($"[data-tool='{Tool}']");
        await Expect(tool).ToHaveAttributeAsync("data-tool-enabled", "true");
        await Expect(tool).ToHaveAttributeAsync("data-tool-class", "Read");
        await Expect(tool.Locator("[data-tool-discloses]")).ToContainTextAsync("go to Claude");

        // The local model hands over: the answer is the target's, word for word, and the window says who and why.
        await SayAsync("Une question difficile sur Kant");
        var call = Newest.Locator($"[data-conversation-tool='{Tool}']");
        await Expect(call).ToHaveAttributeAsync("data-tool-outcome", "Done");
        await Expect(Newest.Locator("[data-conversation-handoff]")).ToHaveAttributeAsync("data-conversation-handoff", "Claude");
        await Expect(Newest.Locator("[data-conversation-handoff]")).ToHaveAttributeAsync("data-handoff-forced", "false");
        await Expect(Newest.Locator("[data-conversation-handoff-text]")).ToHaveTextAsync("Handed over to Claude: Cette question demande un raisonnement long.");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Ici claude-opus-5-5, dans le cloud. Demande reçue : Une question difficile sur Kant");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Mémoire : non");
        await Expect(Newest.Locator("[data-conversation-answer]")).Not.ToContainTextAsync("Résultat de l'outil");
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-model", "claude-opus-5-5");
        await Expect(Newest.Locator("[data-conversation-meta]")).ToContainTextAsync("Anthropic");

        // "Ask Claude": straight to the target, with the recent conversation and without the formula.
        await SayAsync("Ask Claude why the sky is blue");
        await Expect(Newest.Locator("[data-conversation-tool]")).ToHaveCountAsync(0);
        await Expect(Newest.Locator("[data-conversation-handoff]")).ToHaveAttributeAsync("data-handoff-forced", "true");
        await Expect(Newest.Locator("[data-conversation-handoff-text]")).ToHaveTextAsync("Asked Claude directly, at your request");
        // The other tests leave turns in the history of the host: only "some history" is sure.
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Demande reçue : why the sky is blue Historique :");
        await Expect(Newest.Locator("[data-conversation-answer]")).Not.ToContainTextAsync("Historique : 0 message(s)");
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-model", "claude-opus-5-5");

        // The target of the tasks is named, and is not there yet.
        await SayAsync("Ask Claude Code to tidy the repository");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Claude Code, is not available yet");
        await Expect(Newest.Locator("[data-conversation-handoff]")).ToHaveAttributeAsync("data-handoff-failure", "NoTasksTarget");

        // Without a target again, the tool is withdrawn.
        await ChooseAsync(string.Empty);
        await Expect(Settings.Locator("[data-handoff-no-target]")).ToBeVisibleAsync();
        await OpenSettingsAsync("Tools.Catalog");
        await Expect(Settings.Locator("[data-tools-list]")).ToBeVisibleAsync();
        await Expect(Settings.Locator($"[data-tool='{Tool}']")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task The_memory_goes_to_the_target_only_when_its_box_is_ticked()
    {
        await OpenSettingsAsync("Conversation.Memory");
        await Settings.Locator("[data-memory-add-text]").FillAsync("L'utilisateur aime le thé.");
        await Settings.Locator("[data-memory-add]").ClickAsync();
        await Expect(Settings.Locator("[data-memory-fact]")).ToHaveCountAsync(1);
        await AddClaudeAsync("sk-ant-simulated");
        await ChooseAsync("anthropic");

        await SayAsync("Ask Claude what I like");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Mémoire : non");

        await OpenSettingsAsync("Llm.HandOff");
        await Settings.Locator("[data-handoff-memory]").CheckAsync();
        await SayAsync("Ask Claude what I like, again");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Mémoire : oui");
    }

    [Test]
    public async Task A_target_that_refuses_the_key_is_a_cause_told_not_a_fault()
    {
        await AddClaudeAsync("refused");
        await ChooseAsync("anthropic");

        // By the tool: the cause goes back to the local model, which answers.
        await SayAsync("Une question difficile sur Kant");
        var call = Newest.Locator($"[data-conversation-tool='{Tool}']");
        await Expect(call).ToHaveAttributeAsync("data-tool-outcome", "Failed");
        await Expect(Newest.Locator("[data-conversation-handoff]")).ToHaveAttributeAsync("data-handoff-failure", "KeyRejected");
        await Expect(Newest.Locator("[data-conversation-handoff-text]")).ToHaveTextAsync("The hand-off did not happen (key refused)");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Résultat de l'outil, Monsieur : The tool failed: The hand-off did not happen: Claude refused the key.");
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-model", "qwen3:14b");

        // Forced: Jarvis says the cause and does not answer in the place of the target.
        await SayAsync("Ask Claude why the sky is blue");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToHaveTextAsync("The hand-off did not happen: Claude refused the key.");
        await Expect(Newest.Locator("[data-conversation-handoff]")).ToHaveAttributeAsync("data-handoff-failure", "KeyRejected");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
    }

    private async Task OpenSettingsAsync(string page)
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-settings");
        await Settings.Locator($"[data-settings-page='{page}']").ClickAsync();
    }

    /// <summary>Adds the simulated Anthropic provider with <paramref name="key"/> in the vault, and saves the providers.</summary>
    private async Task AddClaudeAsync(string key)
    {
        await OpenSettingsAsync("Llm.Providers");
        await Settings.Locator("[data-field='add-kind']").SelectOptionAsync("Anthropic");
        await Settings.Locator("[data-provider-add]").ClickAsync();
        await Expect(Claude).ToHaveCountAsync(1);
        await Claude.Locator(".secret-field-input").FillAsync(key);
        await Claude.Locator(".secret-field-save").ClickAsync();
        await Expect(Claude.Locator(".secret-field-delete")).ToBeVisibleAsync();
        await Settings.Locator("[data-providers-save]").ClickAsync();
        await Expect(Settings.Locator("[data-providers-save]")).ToBeDisabledAsync();

        // A cloud provider without a price cannot be a target (ADR 0020, section 6): the known one is taken.
        await Settings.Locator("[data-settings-page='Llm.Spend']").ClickAsync();
        var row = Settings.Locator("[data-spend-provider='anthropic']");
        await Expect(row).ToBeVisibleAsync();
        if (await row.GetAttributeAsync("data-spend-priced") != "true")
        {
            await row.Locator("[data-spend-known]").ClickAsync();
        }

        await Expect(row).ToHaveAttributeAsync("data-spend-priced", "true");
    }

    private async Task ChooseAsync(string providerId)
    {
        await OpenSettingsAsync("Llm.HandOff");
        await Settings.Locator("[data-handoff-questions]").SelectOptionAsync(providerId);
        await Expect(Settings.Locator("[data-handoff-questions]")).ToHaveValueAsync(providerId);
        await Expect(Settings.Locator(providerId.Length == 0 ? "[data-handoff-no-target], [data-handoff-no-provider]" : $"[data-handoff-target='{providerId}']")).ToBeVisibleAsync();
    }

    /// <summary>Back to no target, no cloud provider, no key and an empty memory: the host keeps them between the tests.</summary>
    private async Task ResetAsync()
    {
        await OpenSettingsAsync("Llm.HandOff");
        var questions = Settings.Locator("[data-handoff-questions]");
        await Expect(questions).ToBeVisibleAsync();
        if (await questions.InputValueAsync() != string.Empty)
        {
            await questions.SelectOptionAsync(string.Empty);
            await Expect(questions).ToHaveValueAsync(string.Empty);
        }

        var memory = Settings.Locator("[data-handoff-memory]");
        if (await memory.IsCheckedAsync())
        {
            await memory.UncheckAsync();
        }

        // The price and the count of the month, while the provider is still there to show its row.
        await Settings.Locator("[data-settings-page='Llm.Spend']").ClickAsync();
        await Expect(Settings.Locator("[data-spend-count]")).ToBeVisibleAsync();
        var price = Settings.Locator("[data-spend-provider='anthropic'] [data-spend-remove]");
        if (await price.CountAsync() == 1)
        {
            await price.ClickAsync();
            await Expect(price).ToHaveCountAsync(0);
        }

        if (await Settings.Locator("[data-spend-reset]").CountAsync() == 1)
        {
            await Settings.Locator("[data-spend-reset]").ClickAsync();
            await Settings.Locator("[data-spend-reset-confirm]").ClickAsync();
            await Expect(Settings.Locator("[data-spend-count]")).ToHaveAttributeAsync("data-spend-count", "0");
        }

        await Settings.Locator("[data-settings-page='Llm.Providers']").ClickAsync();
        await Expect(Settings.Locator("[data-provider-id='local']")).ToBeVisibleAsync();
        if (await Claude.CountAsync() == 1)
        {
            if (await Claude.Locator(".secret-field-delete").CountAsync() == 1)
            {
                await Claude.Locator(".secret-field-delete").ClickAsync();
                await Expect(Claude.Locator(".secret-field-delete")).ToHaveCountAsync(0);
            }

            await Claude.Locator("[data-provider-remove]").ClickAsync();
            await Expect(Claude).ToHaveCountAsync(0);
            await Settings.Locator("[data-providers-save]").ClickAsync();
            await Expect(Settings.Locator("[data-providers-save]")).ToBeDisabledAsync();
        }

        await Settings.Locator("[data-settings-page='Conversation.Memory']").ClickAsync();
        await Expect(Settings.Locator("[data-memory-add-text]")).ToBeVisibleAsync();
        var erase = Settings.Locator("[data-memory-erase]");
        if (await erase.IsEnabledAsync())
        {
            await erase.ClickAsync();
            await Settings.Locator("[data-memory-erase-confirm]").ClickAsync();
            await Expect(Settings.Locator("[data-memory-fact]")).ToHaveCountAsync(0);
        }

        await CloseDialogsAsync();
    }

    private async Task SayAsync(string request)
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-conversation");
        await Expect(Conversation.Locator("[data-conversation-window]")).ToBeVisibleAsync();

        // The newest turn is the first one: wait for the new one before reading its outcome.
        var turns = Conversation.Locator("[data-conversation-turn]");
        var before = await turns.CountAsync();
        await Conversation.Locator("[data-conversation-text]").FillAsync(request);
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");
        await Expect(turns).ToHaveCountAsync(before + 1);
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 15_000 });
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 10_000 });
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
}
