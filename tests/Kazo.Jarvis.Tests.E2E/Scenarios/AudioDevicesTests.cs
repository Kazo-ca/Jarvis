using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The audio devices on the Web host (work item stage1/8-audio-devices, US1.13): mute from the tray menu,
/// microphone unplugged, choice of microphone and output in the settings, test sound.
/// </summary>
[TestFixture]
public sealed class AudioDevicesTests : PageTest
{
    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Orb => Page.Locator(".orb-frame [data-look]");

    private ILocator MicrophoneOpen => Page.Locator("#microphone-open");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await Expect(MicrophoneOpen).ToHaveAttributeAsync("data-device", "simulated-desk");
    }

    [TearDown]
    public async Task PutTheDevicesBack()
    {
        // The Web host keeps its state for the whole run: leave a plugged, unmuted Windows default microphone.
        // The settings dialog covers the page buttons: reset its choices and close it first.
        if (await Settings.CountAsync() == 1)
        {
            await Settings.Locator("[data-audio-output]").SelectOptionAsync("");
            if (await Settings.Locator("[data-audio-microphone]").IsEnabledAsync())
            {
                await Settings.Locator("[data-audio-microphone]").SelectOptionAsync("");
            }

            // The window remembers its page: leave it on the language, where the other tests expect it.
            await Settings.Locator("[data-settings-page='Settings.Language']").ClickAsync();
            await Settings.Locator(".dialog-close").ClickAsync();
            await Expect(Settings).ToHaveCountAsync(0);
        }

        await Page.ClickAsync("#plug-microphones");
        if (await Page.Locator("#toggle-mute").TextContentAsync() is { } text && text.StartsWith("Unmute", StringComparison.Ordinal))
        {
            await Page.ClickAsync("#toggle-mute");
        }

        await Expect(MicrophoneOpen).ToHaveAttributeAsync("data-device", "simulated-desk");
    }

    [Test]
    public async Task Muting_closes_the_microphone_and_shows_the_muted_orb_then_unmuting_opens_it_again()
    {
        await Page.ClickAsync("#toggle-mute");

        await Expect(Orb).ToHaveAttributeAsync("data-look", "MicrophoneMuted");
        await Expect(MicrophoneOpen).ToHaveAttributeAsync("data-device", "");

        // An activation is refused while the microphone is muted.
        await Page.ClickAsync("#simulate-hotkey");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");

        await Page.ClickAsync("#toggle-mute");

        await Expect(Orb).ToHaveAttributeAsync("data-look", "Idle");
        await Expect(MicrophoneOpen).ToHaveAttributeAsync("data-device", "simulated-desk");
    }

    [Test]
    public async Task Unplugging_every_microphone_shows_the_error_until_one_comes_back()
    {
        await Page.ClickAsync("#unplug-microphones");

        await Expect(Orb).ToHaveAttributeAsync("data-look", "Error");
        await Expect(MicrophoneOpen).ToHaveAttributeAsync("data-device", "");

        await Page.ClickAsync("#plug-microphones");

        await Expect(Orb).ToHaveAttributeAsync("data-look", "Idle");
        await Expect(MicrophoneOpen).ToHaveAttributeAsync("data-device", "simulated-desk");
    }

    [Test]
    public async Task The_settings_page_switches_the_microphone_and_shows_its_level()
    {
        await OpenAudioSettings();

        var microphone = Settings.Locator("[data-audio-microphone]");
        await Expect(microphone.Locator("option").First).ToHaveTextAsync("Windows default (Simulated desk microphone)");
        await Expect(Settings.Locator(".settings-field-hint").First).ToHaveTextAsync("Listening through Simulated desk microphone.");

        await microphone.SelectOptionAsync("simulated-headset");

        await Expect(MicrophoneOpen).ToHaveAttributeAsync("data-device", "simulated-headset");
        await Expect(Settings.Locator(".settings-field-hint").First).ToHaveTextAsync("Listening through Simulated headset.");

        // The simulated microphone hums: the meter moves above zero.
        await Expect(Settings.Locator("[data-audio-level]")).Not.ToHaveAttributeAsync("value", "0");
    }

    [Test]
    public async Task The_mute_box_of_the_settings_page_mutes_like_the_tray_menu()
    {
        await OpenAudioSettings();

        await Settings.Locator("[data-audio-mute]").CheckAsync();

        await Expect(Orb).ToHaveAttributeAsync("data-look", "MicrophoneMuted");
        await Expect(Settings.Locator("[data-audio-microphone]")).ToBeDisabledAsync();
        await Expect(Settings.Locator(".settings-field-hint").First).ToContainTextAsync("The microphone is muted");

        await Settings.Locator("[data-audio-mute]").UncheckAsync();

        await Expect(Orb).ToHaveAttributeAsync("data-look", "Idle");
    }

    [Test]
    public async Task The_test_button_plays_the_chime_on_the_chosen_output()
    {
        await OpenAudioSettings();
        var played = Page.Locator("#output-played");
        var before = int.Parse(await played.GetAttributeAsync("data-count") ?? "0", System.Globalization.CultureInfo.InvariantCulture);

        await Settings.Locator("[data-audio-output]").SelectOptionAsync("simulated-headphones");
        await Settings.Locator("[data-audio-test]").ClickAsync();

        await Expect(played).ToHaveAttributeAsync("data-count", (before + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Expect(played).ToHaveAttributeAsync("data-device", "simulated-headphones");
    }

    private async Task OpenAudioSettings()
    {
        await Page.ClickAsync("#open-settings");
        await Expect(Settings).ToHaveCountAsync(1);
        await Settings.Locator("[data-settings-page='Audio.Devices']").ClickAsync();
        await Expect(Settings.Locator(".settings-frame-title")).ToHaveTextAsync("Audio");
    }
}
