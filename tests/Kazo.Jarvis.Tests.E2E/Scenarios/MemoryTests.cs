using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The memory on the Web host (work item stage2/8-memory, ADR 0019, US2.4): a fact drawn from what the user said
/// shows on the « Mémoire » page and is recalled to the model, « souviens-toi que… » and « oublie que… » go through
/// their tools, a fact is typed, pinned and erased on the page, a secret is refused, and the switches cut what is
/// kept and what is sent. The model is simulated: it draws a fact from « je m'appelle … » and « j'habite à … », and
/// says back the memory block it received on « que retiens-tu ».
/// </summary>
[TestFixture]
public sealed class MemoryTests : PageTest
{
    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Newest => Conversation.Locator("[data-conversation-turn]").First;

    private ILocator Facts => Settings.Locator("[data-memory-fact]");

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
    public async Task A_fact_drawn_from_what_the_user_said_is_shown_and_recalled_to_the_model()
    {
        await OpenMemoryAsync();
        await Expect(Settings.Locator("[data-memory-none]")).ToBeVisibleAsync();
        await Expect(Settings.Locator("[data-memory-no-summary]")).ToBeVisibleAsync();
        await Expect(Settings.Locator("[data-memory-enabled]")).ToBeCheckedAsync();
        await Expect(Settings.Locator("[data-memory-automatic]")).ToBeCheckedAsync();
        await Expect(Settings.Locator("[data-memory-silence]")).ToHaveValueAsync("120");

        await SayAsync("Bonsoir, je m'appelle Benoit");

        // Drawn in the background, after the answer: the page shows it when it arrives.
        await OpenMemoryAsync();
        var fact = Facts.Filter(new() { HasText = "L'utilisateur s'appelle Benoit." });
        await Expect(fact).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Expect(fact).ToHaveAttributeAsync("data-memory-source", "Auto");
        await Expect(fact).ToHaveAttributeAsync("data-memory-sent", "true");

        await SayAsync("Que retiens-tu ?");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("L'utilisateur s'appelle Benoit.");

        // Erased on the page: the model no longer receives it.
        await OpenMemoryAsync();
        await fact.Locator("[data-memory-remove]").ClickAsync();
        await Expect(Facts).ToHaveCountAsync(0);
        await SayAsync("Que retiens-tu ?");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Je ne retiens rien");
    }

    [Test]
    public async Task Remember_and_forget_go_through_their_tools_without_a_confirmation()
    {
        await SayAsync("Souviens-toi que l'utilisateur aime le thé vert");
        var remember = Newest.Locator("[data-conversation-tool='remember_fact']");
        await Expect(remember).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Page.Locator("[data-dialog-key='Permissions.Confirmation']")).ToHaveCountAsync(0);

        await OpenMemoryAsync();
        var fact = Facts.Filter(new() { HasText = "l'utilisateur aime le thé vert" });
        await Expect(fact).ToHaveAttributeAsync("data-memory-source", "Asked");

        await SayAsync("Que sais-tu sur le thé ?");
        await Expect(Newest.Locator("[data-conversation-tool='search_memory']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("thé vert");

        await SayAsync("Oublie que l'utilisateur aime le thé vert");
        await Expect(Newest.Locator("[data-conversation-tool='forget_fact']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await OpenMemoryAsync();
        await Expect(Facts).ToHaveCountAsync(0);
    }

    [Test]
    public async Task A_fact_is_typed_pinned_and_erased_on_the_page_and_a_secret_is_refused()
    {
        await OpenMemoryAsync();
        await AddAsync("Mon mot de passe est tournesol");
        await Expect(Settings.Locator("[data-memory-problem]")).ToBeVisibleAsync();
        await Expect(Facts).ToHaveCountAsync(0);

        await AddAsync("L'utilisateur préfère les réponses courtes.");
        await AddAsync("L'utilisateur a deux enfants.");
        await Expect(Facts).ToHaveCountAsync(2);
        await Expect(Settings.Locator("[data-memory-problem]")).ToHaveCountAsync(0);
        await Expect(Settings.Locator("[data-memory-add-text]")).ToHaveValueAsync(string.Empty);

        // The same fact twice is kept once.
        await AddAsync("l'utilisateur a deux ENFANTS.");
        await Expect(Facts).ToHaveCountAsync(2);

        var first = Facts.Filter(new() { HasText = "réponses courtes" });
        await Expect(first).ToHaveAttributeAsync("data-memory-source", "Manual");
        await first.Locator("[data-memory-pin]").CheckAsync();
        await Expect(first).ToHaveAttributeAsync("data-memory-pinned", "true");

        // « Tout effacer » asks first.
        await Settings.Locator("[data-memory-erase]").ClickAsync();
        await Settings.Locator("[data-memory-erase-cancel]").ClickAsync();
        await Expect(Facts).ToHaveCountAsync(2);
        await Settings.Locator("[data-memory-erase]").ClickAsync();
        await Settings.Locator("[data-memory-erase-confirm]").ClickAsync();
        await Expect(Facts).ToHaveCountAsync(0);
        await Expect(Settings.Locator("[data-memory-none]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task The_switches_cut_what_is_kept_and_what_is_sent()
    {
        await OpenMemoryAsync();
        await AddAsync("L'utilisateur habite à Québec.");

        // The automatic memory off: nothing is drawn from what the user says.
        await Settings.Locator("[data-memory-automatic]").UncheckAsync();
        await SayAsync("Bonsoir, je m'appelle Benoit");
        await SayAsync("Que retiens-tu ?");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("L'utilisateur habite à Québec.");
        await Expect(Newest.Locator("[data-conversation-answer]")).Not.ToContainTextAsync("Benoit");
        await OpenMemoryAsync();
        await Expect(Facts).ToHaveCountAsync(1);

        // The memory off: nothing is sent, and what is kept stays visible.
        await Settings.Locator("[data-memory-enabled]").UncheckAsync();
        await Expect(Settings.Locator("[data-memory-automatic]")).ToBeDisabledAsync();
        await Expect(Facts.First).ToHaveAttributeAsync("data-memory-sent", "false");
        await SayAsync("Que retiens-tu ?");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("Je ne retiens rien");
        await OpenMemoryAsync();
        await Expect(Facts).ToHaveCountAsync(1);

        // The silence is kept within its bounds.
        var silence = Settings.Locator("[data-memory-silence]");
        await silence.FillAsync("1");
        await silence.BlurAsync();
        await Expect(silence).ToHaveValueAsync("5");
    }

    private async Task OpenMemoryAsync()
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-settings");
        await Settings.Locator("[data-settings-page='Conversation.Memory']").ClickAsync();
        await Expect(Settings.Locator("[data-memory-add-text]")).ToBeVisibleAsync();
    }

    private async Task AddAsync(string fact)
    {
        await Settings.Locator("[data-memory-add-text]").FillAsync(fact);
        await Settings.Locator("[data-memory-add]").ClickAsync();
    }

    /// <summary>Back to an empty memory with its default settings: the host keeps both between the tests.</summary>
    private async Task ResetAsync()
    {
        await OpenMemoryAsync();
        var enabled = Settings.Locator("[data-memory-enabled]");
        if (!await enabled.IsCheckedAsync())
        {
            await enabled.CheckAsync();
        }

        var automatic = Settings.Locator("[data-memory-automatic]");
        await Expect(automatic).ToBeEnabledAsync();
        if (!await automatic.IsCheckedAsync())
        {
            await automatic.CheckAsync();
        }

        var silence = Settings.Locator("[data-memory-silence]");
        if (await silence.InputValueAsync() != "120")
        {
            await silence.FillAsync("120");
            await silence.BlurAsync();
            await Expect(silence).ToHaveValueAsync("120");
        }

        var erase = Settings.Locator("[data-memory-erase]");
        if (await erase.IsEnabledAsync())
        {
            await erase.ClickAsync();
            await Settings.Locator("[data-memory-erase-confirm]").ClickAsync();
        }

        await Expect(Facts).ToHaveCountAsync(0);
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
