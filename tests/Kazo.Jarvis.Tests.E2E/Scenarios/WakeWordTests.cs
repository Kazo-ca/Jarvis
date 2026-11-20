using System.Globalization;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The wake word on the Web host (work item stage1/9-wake-word, US1.5): a simulated "hey jarvis" starts
/// listening, and the settings page shows the live score, the threshold and the last detection.
/// </summary>
[TestFixture]
public sealed class WakeWordTests : PageTest
{
    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await Expect(Page.Locator("#microphone-open")).ToHaveAttributeAsync("data-device", "simulated-desk");
    }

    [TearDown]
    public async Task BackToIdle()
    {
        // The Web host keeps its state for the whole run: put the threshold back and leave the assistant idle.
        if (await Settings.CountAsync() == 1)
        {
            await SetThreshold("0.5");
            await Settings.Locator("[data-settings-page='Settings.Language']").ClickAsync();
            await Settings.Locator(".dialog-close").ClickAsync();
            await Expect(Settings).ToHaveCountAsync(0);
        }

        if (await Root.GetAttributeAsync("data-phase") == "Listening")
        {
            // Nothing ends listening on the Web host before work item 10: the hotkey cancels it.
            await Page.Locator("#simulate-hotkey").DispatchEventAsync("click");
        }

        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
    }

    [Test]
    public async Task Saying_hey_jarvis_starts_listening()
    {
        // A dialog left open by another scenario may cover the page buttons: dispatch the click.
        await Page.Locator("#simulate-wake-word").DispatchEventAsync("click");

        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Expect(Page.Locator(".orb-frame [data-look]")).ToHaveAttributeAsync("data-look", "Listening");
    }

    [Test]
    public async Task The_settings_page_shows_the_live_score_and_the_last_detection()
    {
        await OpenWakeWordSettings();
        var score = Settings.Locator("[data-wake-score]");
        await Expect(Settings.Locator("[data-wake-state]")).ToHaveAttributeAsync("data-wake-state", "Active");
        await Expect(score).ToHaveAttributeAsync("value", "0.02");

        await Page.Locator("#simulate-wake-word").DispatchEventAsync("click");

        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Expect(Settings.Locator("[data-wake-state]")).ToHaveAttributeAsync("data-wake-state", "Paused");
        await Expect(Settings.Locator("[data-wake-detection]")).ToHaveAttributeAsync("data-wake-detection", "0.6");
        await Expect(Settings.Locator("[data-wake-peak]")).ToHaveAttributeAsync("data-wake-peak", "0.6");
    }

    [Test]
    public async Task A_higher_threshold_ignores_a_weak_hey_jarvis()
    {
        await OpenWakeWordSettings();

        await SetThreshold("0.95");

        await Expect(Settings.Locator("[data-wake-threshold-value]")).ToHaveTextAsync(0.95.ToString("0.00", CultureInfo.CurrentCulture));
        await Expect(Settings.Locator("[data-wake-threshold-mark]")).ToContainTextAsync("threshold");

        // The simulated "hey jarvis" peaks at 0.97: it still passes 0.95, and the detection keeps that score.
        await Page.Locator("#simulate-wake-word").DispatchEventAsync("click");
        await Expect(Settings.Locator("[data-wake-detection]")).ToHaveAttributeAsync("data-wake-detection", "0.97");
    }

    private async Task SetThreshold(string value)
    {
        var slider = Settings.Locator("[data-wake-threshold]");
        await slider.EvaluateAsync(
            "(input, value) => { input.value = value; input.dispatchEvent(new Event('change', { bubbles: true })); }",
            value);
        await Expect(slider).ToHaveValueAsync(value);
    }

    private async Task OpenWakeWordSettings()
    {
        await Page.ClickAsync("#open-settings");
        await Expect(Settings).ToHaveCountAsync(1);
        await Settings.Locator("[data-settings-page='Voice.WakeWord']").ClickAsync();
        await Expect(Settings.Locator(".settings-frame-title")).ToHaveTextAsync("Wake word");
    }
}
