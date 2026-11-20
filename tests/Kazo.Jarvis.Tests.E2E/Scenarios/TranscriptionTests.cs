using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// A spoken request on the Web host (work item stage1/10-transcription, US1.7): the simulated voice detector
/// "speaks", the silence ends the utterance, the simulated Whisper text is published and shown on the
/// transcription page.
/// </summary>
[TestFixture]
public sealed class TranscriptionTests : PageTest
{
    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
    }

    [TearDown]
    public async Task BackToIdle()
    {
        if (await Settings.CountAsync() == 1)
        {
            await Settings.Locator("[data-settings-page='Settings.Language']").ClickAsync();
            await Settings.Locator(".dialog-close").ClickAsync();
        }

        // Nothing answers yet (work item 13): a thinking turn is interrupted, then the new listening cancelled.
        if (await Root.GetAttributeAsync("data-phase") == "Thinking")
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
    public async Task A_phrase_said_after_the_hotkey_is_transcribed_and_Jarvis_thinks()
    {
        var before = int.Parse(await Page.Locator("#last-transcription").GetAttributeAsync("data-turn") ?? "0", System.Globalization.CultureInfo.InvariantCulture);

        await Page.ClickAsync("#simulate-hotkey");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Page.ClickAsync("#say-phrase");

        await Expect(Root).ToHaveAttributeAsync("data-phase", "Thinking", new() { Timeout = 10_000 });
        await Expect(Page.Locator("#last-transcription")).Not.ToHaveAttributeAsync("data-turn", before.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Expect(Page.Locator("#last-transcription")).ToHaveTextAsync("What time is it?");

        // The settings page shows the model and the last request heard.
        await Page.ClickAsync("#simulate-hotkey");
        await Page.ClickAsync("#simulate-hotkey");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
        await Page.ClickAsync("#open-settings");
        await Settings.Locator("[data-settings-page='Voice.Transcription']").ClickAsync();
        await Expect(Settings.Locator("[data-transcription-status]")).ToHaveAttributeAsync("data-transcription-status", "Ready");
        await Expect(Settings.Locator("[data-transcription-heard]")).ToHaveTextAsync("What time is it?");
        await Expect(Settings.Locator("[data-transcription-measure]")).ToContainTextAsync("Silero");
    }
}
