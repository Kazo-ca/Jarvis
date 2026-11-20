using System.Globalization;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The voices on the Web host (work item stage1/12-speech, US1.9 to US1.12): the voices page lists the voices
/// of each language and says a sentence, the default voice of a language is saved, a cloud voice appears with
/// its key and says the text leaves the PC, a spoken answer lights the orb and an interruption cuts it.
/// </summary>
[TestFixture]
public sealed class VoicesTests : PageTest
{
    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Spoken => Page.Locator("#voice-spoken");

    private ILocator Played => Page.Locator("#output-played");

    private ILocator Voice => Settings.Locator("[data-voices-voice]");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");

        // The dialogs live in the host: a window another test left open (updates, settings) would cover the buttons.
        var dialogs = Page.Locator(".dialog-layer");
        for (var open = await dialogs.CountAsync(); open > 0; open--)
        {
            await dialogs.Last.Locator(".dialog-close").ClickAsync();
            await Expect(dialogs).ToHaveCountAsync(open - 1);
        }
    }

    [TearDown]
    public async Task CloseTheSettings()
    {
        // The dialogs live in the host, not in the page: an open window would cover the next test's buttons.
        if (await Settings.CountAsync() == 1)
        {
            await Settings.Locator("[data-settings-page='Settings.Language']").ClickAsync();
            await Settings.Locator(".dialog-close").ClickAsync();
            await Expect(Settings).ToHaveCountAsync(0);
        }
    }

    [Test]
    public async Task The_page_shows_the_voice_of_each_language_and_says_the_sample()
    {
        await OpenVoicesAsync();
        await Expect(Voice).ToHaveValueAsync("piper:en_GB-alan-medium");
        await Expect(Settings.Locator("[data-voices-is-default]")).ToBeVisibleAsync();
        var before = await CountAsync(Spoken);

        await Settings.Locator("[data-voices-listen]").ClickAsync();

        await Expect(Spoken).ToHaveAttributeAsync("data-count", (before + 1).ToString(CultureInfo.InvariantCulture));
        await Expect(Spoken).ToHaveTextAsync("piper:en_GB-alan-medium@1.0 Good evening, sir. All systems are operational.");

        await Settings.Locator("[data-voices-language='fr']").CheckAsync();

        await Expect(Settings.Locator("[data-voices-sentence]")).ToHaveValueAsync("Bonsoir, Monsieur. Tous les systèmes sont opérationnels.");
        await Expect(Voice.Locator("option")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task A_voice_and_speed_become_the_default_of_their_language()
    {
        await OpenVoicesAsync();
        await Settings.Locator("[data-voices-language='fr']").CheckAsync();
        try
        {
            await Voice.SelectOptionAsync("piper:fr_FR-siwis-medium");
            await Settings.Locator("[data-voices-speed]").FillAsync("1.3");
            await Expect(Settings.Locator("[data-voices-is-default]")).ToHaveCountAsync(0);

            await Settings.Locator("[data-voices-default]").ClickAsync();
            await Expect(Settings.Locator("[data-voices-is-default]")).ToHaveTextAsync("Default voice for French");
            await Settings.Locator("[data-voices-listen]").ClickAsync();

            await Expect(Spoken).ToContainTextAsync("piper:fr_FR-siwis-medium@1.3 Bonsoir");

            // Back to English then French: the saved choice comes back.
            await Settings.Locator("[data-voices-language='en']").CheckAsync();
            await Settings.Locator("[data-voices-language='fr']").CheckAsync();
            await Expect(Voice).ToHaveValueAsync("piper:fr_FR-siwis-medium");
            await Expect(Settings.Locator("[data-voices-speed-value]")).ToHaveTextAsync("× 1.3");
        }
        finally
        {
            // Other tests expect the default French voice.
            await Voice.SelectOptionAsync("piper:fr_FR-tom-medium");
            await Settings.Locator("[data-voices-speed]").FillAsync("1");
            await Settings.Locator("[data-voices-default]").ClickAsync();
            await Expect(Settings.Locator("[data-voices-is-default]")).ToBeVisibleAsync();
        }
    }

    [Test]
    public async Task A_cloud_voice_appears_with_its_key_and_warns_that_the_text_leaves_the_computer()
    {
        await OpenVoicesAsync();
        var key = Settings.Locator("[data-secret-ref='voice.openai.apikey']");
        await Expect(Voice.Locator("option[value='openai:nova']")).ToHaveCountAsync(0);

        await key.Locator(".secret-field-input").FillAsync("sk-simulated");
        await key.Locator(".secret-field-save").ClickAsync();
        try
        {
            await Expect(Voice.Locator("option[value='openai:nova']")).ToHaveCountAsync(1);
            await Voice.SelectOptionAsync("openai:nova");

            await Expect(Settings.Locator("[data-voices-cloud-warning]")).ToContainTextAsync("sent to OpenAI");
        }
        finally
        {
            await key.Locator(".secret-field-delete").ClickAsync();
            await Expect(Voice.Locator("option[value='openai:nova']")).ToHaveCountAsync(0);
        }
    }

    [Test]
    public async Task A_spoken_answer_lights_the_orb_then_returns_to_idle()
    {
        await Page.ClickAsync("#simulate-answer");

        await Expect(Root).ToHaveAttributeAsync("data-phase", "Speaking");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 10_000 });
        await Expect(Spoken).ToContainTextAsync("a second, longer sentence");
    }

    [Test]
    public async Task An_interruption_cuts_the_voice_and_listens_again()
    {
        await Page.ClickAsync("#simulate-answer");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Speaking");

        // The hotkey while Jarvis speaks (US1.12): the sound stops at once, a new turn listens.
        await Page.ClickAsync("#simulate-hotkey");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Page.WaitForTimeoutAsync(500);
        var played = await CountAsync(Played);

        // The queued sentence would have played for about two seconds: nothing more is played.
        await Page.WaitForTimeoutAsync(2500);
        Assert.That(await CountAsync(Played), Is.EqualTo(played));
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");

        await Page.ClickAsync("#simulate-hotkey");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
    }

    private async Task OpenVoicesAsync()
    {
        await Page.ClickAsync("#open-settings");
        await Expect(Settings).ToHaveCountAsync(1);
        await Settings.Locator("[data-settings-page='Voice.TestVoice']").ClickAsync();
        await Expect(Settings.Locator(".settings-frame-title")).ToHaveTextAsync("Voices");
    }

    private static async Task<int> CountAsync(ILocator output) =>
        int.Parse(await output.GetAttributeAsync("data-count") ?? "0", CultureInfo.InvariantCulture);
}
