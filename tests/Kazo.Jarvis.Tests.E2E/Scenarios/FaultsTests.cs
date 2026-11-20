using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The named faults on the Web host (work item stage1/15-faults, US1.17): each cause is simulated, the faults window
/// names it with its corrective action and opens the right settings page, and the fault goes away when its cause
/// does or when a turn succeeds.
/// </summary>
[TestFixture]
public sealed class FaultsTests : PageTest
{
    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Orb => Page.Locator(".orb-frame [data-look]");

    private ILocator Fault => Page.Locator("#fault");

    private ILocator Window => Page.Locator("[data-dialog-key='Assistant.Faults']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await Expect(Fault).ToHaveAttributeAsync("data-cause", string.Empty);
    }

    [TearDown]
    public async Task PutEverythingBack()
    {
        // The Web host keeps its state for the whole run: leave every simulated cause repaired, the settings window on
        // the language page, no window open, and no fault.
        if (await Settings.CountAsync() == 1)
        {
            await Settings.Locator("[data-settings-page='Settings.Language']").ClickAsync();
        }

        await CloseAsync(Settings);
        await CloseAsync(Window);
        await RepairAsync("#toggle-model-server", "Start");
        await RepairAsync("#toggle-language-model", "Download");
        await RepairAsync("#toggle-whisper", "Put");
        await RepairAsync("#toggle-voice", "Repair");
        await Page.ClickAsync("#plug-microphones");
        await Page.ClickAsync("#repair-configuration");
        if (await Fault.GetAttributeAsync("data-cause") is { Length: > 0 })
        {
            // A successful turn clears any fault left by a failed test (ADR 0005, section 3).
            await SpeakAsync();
        }

        await Expect(Fault).ToHaveAttributeAsync("data-cause", string.Empty, new() { Timeout = 15_000 });
    }

    [Test]
    public async Task No_microphone_opens_the_window_on_a_refused_activation_and_it_follows_the_microphone()
    {
        await Page.ClickAsync("#unplug-microphones");
        await Expect(Orb).ToHaveAttributeAsync("data-look", "Error");

        // The hotkey does nothing without a microphone: the faults window says why.
        await Page.ClickAsync("#simulate-hotkey");

        await Expect(Window).ToHaveCountAsync(1);
        await Expect(Window.Locator("[data-fault-window]")).ToHaveAttributeAsync("data-fault-cause", "MicrophoneMissing");
        await Expect(Window.Locator("[data-fault-title]")).ToHaveTextAsync("No microphone");
        await Expect(Window.Locator("[data-fault-action]")).ToContainTextAsync("Plug in a microphone");

        await OpensSettingsPageAsync("Audio.Devices", "Audio");

        // The microphone comes back: the window says the problem is solved, the orb is idle again.
        await Page.Locator("#plug-microphones").DispatchEventAsync("click");
        await Expect(Window.Locator("[data-fault-none]")).ToHaveTextAsync("The problem is solved: Jarvis works normally again.");
        await Expect(Orb).ToHaveAttributeAsync("data-look", "Idle");
    }

    [Test]
    public async Task An_unreachable_model_server_is_named_and_cleared_by_the_next_answered_turn()
    {
        await Page.ClickAsync("#toggle-model-server");

        await SpeakAsync();
        await Expect(Fault).ToHaveAttributeAsync("data-cause", "ModelUnreachable");
        await Expect(Fault).ToHaveAttributeAsync("data-origin", "LanguageModel");
        await Expect(Orb).ToHaveAttributeAsync("data-look", "Error");

        await OpenWindowAsync();
        await Expect(Window.Locator("[data-fault-title]")).ToHaveTextAsync("Language model unreachable");
        await Expect(Window.Locator("[data-fault-action]")).ToContainTextAsync("Test the connection");
        await Expect(Window.Locator("[data-fault-detail]")).ToContainTextAsync("Unreachable");
        await OpensSettingsPageAsync("Llm.Providers", "Language models");

        await CloseAsync(Settings);
        await CloseAsync(Window);
        await Page.ClickAsync("#toggle-model-server");
        await SpeakAsync();
        await Expect(Fault).ToHaveAttributeAsync("data-cause", string.Empty, new() { Timeout = 15_000 });
    }

    [Test]
    public async Task A_missing_language_model_sends_to_the_language_models_page()
    {
        await Page.ClickAsync("#toggle-language-model");

        await SpeakAsync();
        await Expect(Fault).ToHaveAttributeAsync("data-cause", "ModelMissing");
        await Expect(Fault).ToHaveAttributeAsync("data-origin", "LanguageModel");

        await OpenWindowAsync();
        await Expect(Window.Locator("[data-fault-title]")).ToHaveTextAsync("Language model missing");
        await Expect(Window.Locator("[data-fault-action]")).ToContainTextAsync("ollama pull");
        await OpensSettingsPageAsync("Llm.Providers", "Language models");
    }

    [Test]
    public async Task A_missing_Whisper_file_sends_to_the_voice_models_page_and_goes_away_when_it_is_back()
    {
        await Page.ClickAsync("#toggle-whisper");

        await SpeakAsync();
        await Expect(Fault).ToHaveAttributeAsync("data-cause", "ModelMissing");
        await Expect(Fault).ToHaveAttributeAsync("data-origin", "Transcription");

        await OpenWindowAsync();
        await Expect(Window.Locator("[data-fault-title]")).ToHaveTextAsync("Transcription model missing");
        await Expect(Window.Locator("[data-fault-detail]")).ToContainTextAsync("Whisper file is missing");
        await OpensSettingsPageAsync("Setup.Models", "Voice models");

        await CloseAsync(Settings);
        await CloseAsync(Window);
        await Page.ClickAsync("#toggle-whisper");
        await SpeakAsync();
        await Expect(Fault).ToHaveAttributeAsync("data-cause", string.Empty, new() { Timeout = 15_000 });
    }

    [Test]
    public async Task A_silent_voice_sends_to_the_voices_page_where_a_voice_heard_clears_it()
    {
        await Page.ClickAsync("#toggle-voice");

        await SpeakAsync();
        await Expect(Fault).ToHaveAttributeAsync("data-cause", "VoiceMissing");
        await Expect(Fault).ToHaveAttributeAsync("data-origin", "Voice");

        await OpenWindowAsync();
        await Expect(Window.Locator("[data-fault-title]")).ToHaveTextAsync("Silent voice");
        await OpensSettingsPageAsync("Voice.TestVoice", "Voices");

        // Repaired, then heard on the voices page: the fault goes away without a turn.
        await Page.Locator("#toggle-voice").DispatchEventAsync("click");
        await Settings.Locator("[data-voices-listen]").ClickAsync();
        await Expect(Window.Locator("[data-fault-none]")).ToBeVisibleAsync();
        await Expect(Fault).ToHaveAttributeAsync("data-cause", string.Empty);
    }

    [Test]
    public async Task An_unreadable_settings_file_is_named_without_a_page_to_open()
    {
        await Page.ClickAsync("#break-configuration");
        await Expect(Fault).ToHaveAttributeAsync("data-cause", "ConfigurationUnreadable");
        await Expect(Orb).ToHaveAttributeAsync("data-look", "Error");

        await OpenWindowAsync();
        await Expect(Window.Locator("[data-fault-title]")).ToHaveTextAsync("Unreadable settings");
        await Expect(Window.Locator("[data-fault-explanation]")).ToContainTextAsync("saves no setting");
        await Expect(Window.Locator("[data-fault-action]")).ToContainTextAsync("config.json");
        await Expect(Window.Locator("[data-fault-open-setting]")).ToHaveCountAsync(0);

        await Page.Locator("#repair-configuration").DispatchEventAsync("click");
        await Expect(Window.Locator("[data-fault-none]")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Without_a_fault_the_window_says_so()
    {
        await OpenWindowAsync();

        await Expect(Window.Locator("[data-fault-none]")).ToHaveTextAsync("No problem right now: Jarvis works normally.");
    }

    /// <summary>A spoken turn from the wake word to its end, whatever the outcome.</summary>
    private async Task SpeakAsync()
    {
        await Page.ClickAsync("#simulate-wake-word");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");
        await Page.ClickAsync("#say-phrase");
        await Expect(Root).Not.ToHaveAttributeAsync("data-phase", "Listening", new() { Timeout = 10_000 });
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
    }

    private async Task OpenWindowAsync()
    {
        await Page.ClickAsync("#open-faults");
        await Expect(Window).ToHaveCountAsync(1);
    }

    /// <summary>The button of the faults window opens the settings on <paramref name="key"/>.</summary>
    private async Task OpensSettingsPageAsync(string key, string title)
    {
        var button = Window.Locator("[data-fault-open-setting]");
        await Expect(button).ToHaveAttributeAsync("data-fault-open-setting", key);
        await Expect(button).ToHaveTextAsync($"Open the “{title}” page");

        await button.ClickAsync();

        await Expect(Settings).ToHaveCountAsync(1);
        await Expect(Settings.Locator($"[data-settings-page='{key}']")).ToHaveAttributeAsync("aria-current", "page");
    }

    private async Task CloseAsync(ILocator dialog)
    {
        if (await dialog.CountAsync() == 1)
        {
            await dialog.Locator(".dialog-close").ClickAsync();
            await Expect(dialog).ToHaveCountAsync(0);
        }
    }

    private async Task RepairAsync(string button, string repairedLabelStart)
    {
        if (await Page.Locator(button).TextContentAsync() is { } text && text.StartsWith(repairedLabelStart, StringComparison.Ordinal))
        {
            await Page.ClickAsync(button);
        }
    }
}
