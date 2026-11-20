using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The language setting (US1.4, ADR 0008, section 6): changed in the settings window of the Web host, it
/// applies to the open window without reloading the page.
/// </summary>
[TestFixture]
public sealed class LanguageTests : PageTest
{
    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Title => Settings.Locator(".settings-frame-title");

    [SetUp]
    public async Task OpenSettings()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Page.Locator("#jarvis-root")).ToHaveAttributeAsync("data-interactive", "true");

        await Page.ClickAsync("#open-settings");
        await Expect(Settings).ToHaveCountAsync(1);
    }

    [TearDown]
    public async Task BackToEnglish()
    {
        // The Web host keeps its settings for the whole run: leave it in English for the other tests.
        var english = Settings.Locator("input[data-language='en']");
        if (await english.CountAsync() == 1 && !await english.IsCheckedAsync())
        {
            await english.CheckAsync();
            await Expect(Title).ToHaveTextAsync("Language");
        }

        await Page.ClickAsync("[data-dialog-key='Settings.Window'] .dialog-close");
    }

    [Test]
    public async Task The_settings_open_on_the_language_proposed_from_Windows()
    {
        await Expect(Title).ToHaveTextAsync("Language");
        await Expect(Settings.Locator("[data-settings-page='Settings.Language']")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Settings.Locator("input[data-language='en']")).ToBeCheckedAsync();
        await Expect(Settings.Locator(".settings-field-hint")).ToContainTextAsync("Proposed from the Windows display language: English");
    }

    [Test]
    public async Task Choosing_French_translates_the_open_window_without_reloading()
    {
        // A marker on the page: it disappears if the page reloads.
        await Page.EvaluateAsync("() => window.notReloaded = true");

        await Settings.Locator("input[data-language='fr']").CheckAsync();

        await Expect(Title).ToHaveTextAsync("Langue");
        await Expect(Settings.Locator("[data-settings-page='Settings.Language']")).ToHaveTextAsync("Langue");
        await Expect(Settings.Locator("input[data-language='fr']")).ToBeCheckedAsync();
        await Expect(Settings.Locator(".settings-field-hint")).ToContainTextAsync("sans redémarrer");
        Assert.That(await Page.EvaluateAsync<bool>("() => window.notReloaded === true"), Is.True);

        await Settings.Locator("input[data-language='en']").CheckAsync();
        await Expect(Title).ToHaveTextAsync("Language");
    }

    [Test]
    public async Task The_shared_style_and_its_fonts_are_served_locally()
    {
        var family = await Title.EvaluateAsync<string>("element => getComputedStyle(element).fontFamily");
        Assert.That(family, Does.StartWith("Rajdhani"));

        // The fonts come from the application, never from the internet (ADR 0011, section 2).
        await Page.EvaluateAsync("() => document.fonts.ready");
        var loaded = await Page.EvaluateAsync<bool>("() => document.fonts.check('600 16px Rajdhani')");
        Assert.That(loaded, Is.True);
    }
}
