using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The spending cap on the Web host (work item stage2/10-spend-cap, ADR 0020, section 6, US2.8): a cloud provider
/// without a price cannot be a target, each answer of the target is counted from its tokens and its price, the
/// count shows on the pages and in the conversation window, Jarvis says once that 80 % is passed and that the cap
/// is reached, and at the cap it stays local until the cap is raised. The simulated cloud answer is 42 tokens
/// sent and 18 written.
/// </summary>
[TestFixture]
public sealed class SpendTests : PageTest
{
    private const string Tool = "hand_off_to_claude";

    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Newest => Conversation.Locator("[data-conversation-turn]").First;

    private ILocator Claude => Settings.Locator("[data-provider-id='anthropic']");

    private ILocator Row => Settings.Locator("[data-spend-provider='anthropic']");

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
    public async Task The_page_shows_a_cap_of_ten_dollars_nothing_spent_and_what_the_count_is_not()
    {
        await OpenSettingsAsync("Llm.Spend");
        await Expect(Settings.Locator(".settings-frame-title")).ToHaveTextAsync("Spending");
        await Expect(Settings.Locator("[data-spend-cap]")).ToHaveValueAsync("10");
        await Expect(Settings.Locator("[data-spend-count]")).ToHaveAttributeAsync("data-spend-count", "0");
        await Expect(Settings.Locator("[data-spend-count]")).ToHaveAttributeAsync("data-spend-reached", "false");
        await Expect(Settings.Locator("[data-spend-count]")).ToContainTextAsync("0.00 USD spent out of 10.00 USD");
        await Expect(Settings.Locator("[data-spend-no-provider]")).ToBeVisibleAsync();
        await Expect(Settings.Locator("[data-spend-reset]")).ToHaveCountAsync(0);
        await Expect(Settings.Locator("[data-spend-limits] li")).ToHaveCountAsync(3);
        await Expect(Settings.Locator("[data-spend-limits]")).ToContainTextAsync("It is an estimate");
        await Expect(Settings.Locator("[data-spend-limits]")).ToContainTextAsync("cloud voices");

        // Nothing spent: the conversation window shows no count.
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-conversation");
        await Expect(Conversation.Locator("[data-conversation-window]")).ToBeVisibleAsync();
        await Expect(Conversation.Locator("[data-conversation-spend]")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task A_cloud_provider_without_a_price_cannot_be_a_target_until_its_price_is_typed()
    {
        await AddClaudeAsync("sk-ant-simulated", priced: false);

        await OpenSettingsAsync("Llm.HandOff");
        var option = Settings.Locator("[data-handoff-questions] option[value='anthropic']");
        await Expect(option).ToBeDisabledAsync();
        await Expect(option).ToHaveAttributeAsync("data-handoff-unpriced", "true");
        await Expect(option).ToContainTextAsync("no price");
        await Expect(Settings.Locator("[data-handoff-price-note]")).ToBeVisibleAsync();
        await Expect(Settings.Locator("[data-handoff-spend]")).ToContainTextAsync("0.00 USD out of 10.00 USD");

        // The page proposes the price known when this version was published; nothing is fetched.
        await Settings.Locator("[data-settings-page='Llm.Spend']").ClickAsync();
        await Expect(Row).ToHaveAttributeAsync("data-spend-priced", "false");
        await Expect(Row.Locator("[data-spend-unpriced]")).ToBeVisibleAsync();
        await Expect(Row.Locator("[data-spend-known]")).ToContainTextAsync("4 / 20");
        await Row.Locator("[data-spend-known]").ClickAsync();
        await Expect(Row).ToHaveAttributeAsync("data-spend-priced", "true");
        await Expect(Row.Locator("[data-spend-input]")).ToHaveValueAsync("4");
        await Expect(Row.Locator("[data-spend-output]")).ToHaveValueAsync("20");
        await Expect(Row.Locator("[data-spend-known]")).ToHaveCountAsync(0);

        await Settings.Locator("[data-settings-page='Llm.HandOff']").ClickAsync();
        await Expect(option).ToBeEnabledAsync();
        await Expect(Settings.Locator("[data-handoff-price-note]")).ToHaveCountAsync(0);
        await ChooseAsync("anthropic");

        // The price removed afterwards: the choice stays, nothing is handed over, and Jarvis says why.
        await Settings.Locator("[data-settings-page='Llm.Spend']").ClickAsync();
        await Row.Locator("[data-spend-remove]").ClickAsync();
        await Expect(Row).ToHaveAttributeAsync("data-spend-priced", "false");
        await Settings.Locator("[data-settings-page='Llm.HandOff']").ClickAsync();
        await Expect(Settings.Locator("[data-handoff-target-unpriced]")).ToBeVisibleAsync();
        await Settings.Locator("[data-settings-page='Tools.Catalog']").ClickAsync();
        await Expect(Settings.Locator("[data-tools-list]")).ToBeVisibleAsync();
        await Expect(Settings.Locator($"[data-tool='{Tool}']")).ToHaveCountAsync(0);

        await SayAsync("Ask Claude why the sky is blue");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("The hand-off did not happen: the target has no price");
        await Expect(Newest.Locator("[data-conversation-handoff]")).ToHaveAttributeAsync("data-handoff-failure", "NoPrice");
        await Expect(Newest.Locator("[data-conversation-handoff-text]")).ToHaveTextAsync("The hand-off did not happen (the target has no price)");
    }

    [Test]
    public async Task Each_answer_of_the_target_is_counted_and_the_count_shows()
    {
        await AddClaudeAsync("sk-ant-simulated");
        await ChooseAsync("anthropic");

        await SayAsync("Ask Claude why the sky is blue");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Ici claude-opus-5-5, dans le cloud.");

        // 42 tokens at 4 USD and 18 at 20 USD per million: 0.000528 USD.
        var count = Conversation.Locator("[data-conversation-spend]");
        await Expect(count).ToHaveAttributeAsync("data-conversation-spend", "0.000528");
        await Expect(count).ToHaveAttributeAsync("data-spend-reached", "false");
        await Expect(count).ToContainTextAsync("0.0005 USD out of 10.00 USD");

        // The local model is not counted.
        await SayAsync("What time is it?");
        await Expect(count).ToHaveAttributeAsync("data-conversation-spend", "0.000528");

        await OpenSettingsAsync("Llm.Spend");
        await Expect(Settings.Locator("[data-spend-count]")).ToHaveAttributeAsync("data-spend-count", "0.000528");
        await Expect(Settings.Locator("[data-spend-entries]")).ToHaveAttributeAsync("data-spend-entries", "1");
        await Expect(Settings.Locator("[data-spend-entries] li")).ToContainTextAsync("42 tokens in, 18 out");
        await Expect(Settings.Locator("[data-spend-entries] li")).ToContainTextAsync("claude-opus-5-5");
        await Settings.Locator("[data-settings-page='Llm.HandOff']").ClickAsync();
        await Expect(Settings.Locator("[data-handoff-spend]")).ToHaveAttributeAsync("data-handoff-spend", "0.000528");

        // « Erase everything » of the conversation does not lift the count.
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-conversation");
        await Conversation.Locator("[data-conversation-erase]").ClickAsync();
        await Conversation.Locator("[data-conversation-erase-confirm]").ClickAsync();
        await Expect(Conversation.Locator("[data-conversation-turn]")).ToHaveCountAsync(0);
        await Expect(Conversation.Locator("[data-conversation-spend]")).ToHaveAttributeAsync("data-conversation-spend", "0.000528");

        // The user puts it back to zero, after a confirmation.
        await OpenSettingsAsync("Llm.Spend");
        await Settings.Locator("[data-spend-reset]").ClickAsync();
        await Settings.Locator("[data-spend-reset-confirm]").ClickAsync();
        await Expect(Settings.Locator("[data-spend-count]")).ToHaveAttributeAsync("data-spend-count", "0");
    }

    [Test]
    public async Task Jarvis_warns_at_eighty_percent_says_the_cap_is_reached_and_stays_local_until_it_is_raised()
    {
        await AddClaudeAsync("sk-ant-simulated");
        await ChooseAsync("anthropic");

        // A million dollars per million tokens: each simulated answer costs 60 USD. With a cap of 70, the first
        // one passes 80 % and the second one reaches the cap.
        await OpenSettingsAsync("Llm.Spend");
        await TypeAsync(Row.Locator("[data-spend-input]"), "1000000");
        await TypeAsync(Row.Locator("[data-spend-output]"), "1000000");
        await Expect(Row.Locator("[data-spend-known]")).ToBeVisibleAsync();
        await TypeAsync(Settings.Locator("[data-spend-cap]"), "70");
        await Expect(Settings.Locator("[data-spend-count]")).ToContainTextAsync("out of 70.00 USD");

        await SayAsync("Ask Claude why the sky is blue");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Ici claude-opus-5-5, dans le cloud.");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("By the way, more than 80 % of this month's cloud spending cap is used.");
        await Expect(Conversation.Locator("[data-conversation-spend]")).ToHaveAttributeAsync("data-conversation-spend", "60");

        // By the tool this time: the answer that reaches the cap is given, then Jarvis says the cap is reached.
        await SayAsync("Une question difficile sur Kant");
        await Expect(Newest.Locator($"[data-conversation-tool='{Tool}']")).ToHaveAttributeAsync("data-tool-outcome", "Done");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Ici claude-opus-5-5, dans le cloud.");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("This month's cloud spending cap is now reached: I stay local");
        await Expect(Newest.Locator("[data-conversation-answer]")).Not.ToContainTextAsync("more than 80 %");
        var count = Conversation.Locator("[data-conversation-spend]");
        await Expect(count).ToHaveAttributeAsync("data-conversation-spend", "120");
        await Expect(count).ToHaveAttributeAsync("data-spend-reached", "true");
        await Expect(count).ToContainTextAsync("Cap reached, Jarvis stays local.");

        // At the cap: the local model is no longer offered the tool, and a forced hand-off says why.
        await SayAsync("Une question difficile sur Hegel");
        await Expect(Newest.Locator("[data-conversation-tool]")).ToHaveCountAsync(0);
        await Expect(Newest.Locator("[data-conversation-meta]")).ToHaveAttributeAsync("data-conversation-model", "qwen3:14b");
        await SayAsync("Ask Claude why the sky is blue");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToHaveTextAsync("The hand-off did not happen: this month's cloud spending cap is reached. Jarvis stays local until next month, or until the cap is raised on the Spending page.");
        await Expect(Newest.Locator("[data-conversation-handoff]")).ToHaveAttributeAsync("data-handoff-failure", "CapReached");
        await Expect(count).ToHaveAttributeAsync("data-conversation-spend", "120");

        await OpenSettingsAsync("Llm.Spend");
        await Expect(Settings.Locator("[data-spend-count]")).ToHaveAttributeAsync("data-spend-reached", "true");
        await Expect(Settings.Locator("[data-spend-reached-note]")).ToBeVisibleAsync();
        await Expect(Settings.Locator("[data-spend-entries]")).ToHaveAttributeAsync("data-spend-entries", "2");
        await Settings.Locator("[data-settings-page='Llm.HandOff']").ClickAsync();
        await Expect(Settings.Locator("[data-handoff-spend]")).ToHaveAttributeAsync("data-handoff-cap-reached", "true");
        await Expect(Settings.Locator("[data-handoff-questions]")).ToHaveValueAsync("anthropic");
        await Settings.Locator("[data-settings-page='Tools.Catalog']").ClickAsync();
        await Expect(Settings.Locator("[data-tools-list]")).ToBeVisibleAsync();
        await Expect(Settings.Locator($"[data-tool='{Tool}']")).ToHaveCountAsync(0);

        // The user raises the cap: the tool comes back and the target answers again.
        await Settings.Locator("[data-settings-page='Llm.Spend']").ClickAsync();
        await TypeAsync(Settings.Locator("[data-spend-cap]"), "1000");
        await Expect(Settings.Locator("[data-spend-count]")).ToHaveAttributeAsync("data-spend-reached", "false");
        await Settings.Locator("[data-settings-page='Tools.Catalog']").ClickAsync();
        await Expect(Settings.Locator($"[data-tool='{Tool}']")).ToHaveAttributeAsync("data-tool-enabled", "true");
        await SayAsync("Ask Claude why the sky is blue");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Ici claude-opus-5-5, dans le cloud.");
        await Expect(Newest.Locator("[data-conversation-answer]")).Not.ToContainTextAsync("cap");
    }

    [Test]
    public async Task A_cap_out_of_bounds_is_refused_and_said()
    {
        await OpenSettingsAsync("Llm.Spend");
        await TypeAsync(Settings.Locator("[data-spend-cap]"), "-5");
        await Expect(Settings.Locator("[data-spend-problem]")).ToContainTextAsync("The monthly cap goes from 0 to");
        await Expect(Settings.Locator("[data-spend-count]")).ToContainTextAsync("out of 10.00 USD");
    }

    private static async Task TypeAsync(ILocator input, string value)
    {
        await input.FillAsync(value);
        await input.PressAsync("Tab");
    }

    private async Task OpenSettingsAsync(string page)
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-settings");
        await Settings.Locator($"[data-settings-page='{page}']").ClickAsync();
    }

    /// <summary>Adds the simulated Anthropic provider with <paramref name="key"/> in the vault, and saves the providers.</summary>
    private async Task AddClaudeAsync(string key, bool priced = true)
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
        await Expect(Row).ToBeVisibleAsync();
        if (priced)
        {
            await Row.Locator("[data-spend-known]").ClickAsync();
            await Expect(Row).ToHaveAttributeAsync("data-spend-priced", "true");
        }
    }

    private async Task ChooseAsync(string providerId)
    {
        await OpenSettingsAsync("Llm.HandOff");
        await Settings.Locator("[data-handoff-questions]").SelectOptionAsync(providerId);
        await Expect(Settings.Locator("[data-handoff-questions]")).ToHaveValueAsync(providerId);
        await Expect(Settings.Locator(providerId.Length == 0 ? "[data-handoff-no-target], [data-handoff-no-provider]" : $"[data-handoff-target='{providerId}']")).ToBeVisibleAsync();
    }

    /// <summary>Back to no target, no cloud provider, no key, no price, a cap of 10, nothing spent and an empty memory: the host keeps them between the tests.</summary>
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

        if (await Settings.Locator("[data-spend-cap]").InputValueAsync() != "10")
        {
            await TypeAsync(Settings.Locator("[data-spend-cap]"), "10");
            await Expect(Settings.Locator("[data-spend-count]")).ToContainTextAsync("out of 10.00 USD");
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
