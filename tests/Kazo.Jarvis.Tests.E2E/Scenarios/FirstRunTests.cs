using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The first-run wizard (US1.4, stage 1 plan, work item 14) in the Web host, in English and in French: language,
/// audio, models (simulated downloads, simulated RTX 3060) and language model, then the closing screen.
/// </summary>
[TestFixture]
public sealed class FirstRunTests : PageTest
{
    private ILocator Wizard => Page.Locator("[data-dialog-key='Setup.FirstRun']");

    private ILocator Frame => Wizard.Locator("[data-first-run]");

    private ILocator Next => Wizard.Locator("[data-first-run-next]");

    [SetUp]
    public async Task OpenWizard()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Page.Locator("#jarvis-root")).ToHaveAttributeAsync("data-interactive", "true");
        await Page.ClickAsync("#open-first-run");
        await Expect(Wizard).ToHaveCountAsync(1);
    }

    [TearDown]
    public async Task BackToEnglish()
    {
        // The Web host keeps its settings for the whole run: leave it in English for the other tests.
        if (await Wizard.CountAsync() == 0)
        {
            await Page.ClickAsync("#open-first-run");
        }

        var english = Wizard.Locator("input[data-language='en']");
        if (!await english.IsCheckedAsync())
        {
            await english.CheckAsync();
            await Expect(Wizard.Locator("[data-first-run-progress]")).ToHaveTextAsync("Step 1 of 5");
        }

        await Page.ClickAsync("[data-dialog-key='Setup.FirstRun'] .dialog-close");
    }

    [Test]
    public async Task The_whole_wizard_in_English()
    {
        await Expect(Frame).ToHaveAttributeAsync("data-step", "Settings.Language");
        await Expect(Wizard.Locator("[data-first-run-progress]")).ToHaveTextAsync("Step 1 of 5");
        await Expect(Wizard.Locator("[data-first-run-welcome]")).ToContainTextAsync("A few choices before I am at your service");
        await Expect(Wizard.Locator("[data-first-run-back]")).ToBeDisabledAsync();
        await Expect(Wizard.Locator("input[data-language='en']")).ToBeCheckedAsync();

        await Next.ClickAsync();
        await Expect(Frame).ToHaveAttributeAsync("data-step", "Audio.Devices");
        await Expect(Wizard.Locator(".settings-frame-title")).ToHaveTextAsync("Audio");
        await Expect(Wizard.Locator("[data-audio-microphone]")).ToBeVisibleAsync();

        await Next.ClickAsync();
        await Expect(Frame).ToHaveAttributeAsync("data-step", "Setup.Models");
        await Expect(Wizard.Locator("[data-graphics-card]")).ToContainTextAsync("NVIDIA GeForce RTX 3060, 12 GB");
        await Expect(Wizard.Locator("[data-llm-advice]")).ToHaveCountAsync(0);
        await Expect(Row("whisper-large-v3-turbo").Locator("[data-model-select]")).ToBeCheckedAsync();
        await Expect(Row("kokoro").Locator("[data-model-select]")).Not.ToBeCheckedAsync();
        await Expect(Row("whisper-large-v3-turbo")).ToContainTextAsync("574 MB");

        await Wizard.Locator("[data-models-install]").ClickAsync();
        await Expect(Wizard.Locator("[data-models-cancel]")).ToBeVisibleAsync();
        await Expect(Wizard.Locator("[data-models-outcome]")).ToHaveAttributeAsync("data-models-outcome", "Installed", new() { Timeout = 20_000 });
        foreach (var id in new[] { "wake-word", "silero-vad", "whisper-large-v3-turbo", "piper-fr", "piper-en" })
        {
            await Expect(Row(id)).ToHaveAttributeAsync("data-model-state", "Installed");
        }

        await Expect(Row("kokoro")).ToHaveAttributeAsync("data-model-state", "Missing");

        await Next.ClickAsync();
        await Expect(Frame).ToHaveAttributeAsync("data-step", "Llm.Providers");
        await Expect(Wizard.Locator("[data-ollama-search]")).ToHaveAttributeAsync("data-ollama-search", "11434");
        await Expect(Wizard.Locator("[data-ollama-search]")).ToContainTextAsync("Ollama answers on port 11434");

        await Next.ClickAsync();
        await Expect(Frame).ToHaveAttributeAsync("data-step", "done");
        await Expect(Wizard.Locator("[data-first-run-done]")).ToContainTextAsync("Say “Hey Jarvis”");
        await Wizard.Locator("[data-first-run-back]").ClickAsync();
        await Expect(Frame).ToHaveAttributeAsync("data-step", "Llm.Providers");
        await Next.ClickAsync();

        await Wizard.Locator("[data-first-run-finish]").ClickAsync();
        await Expect(Wizard).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Choosing_French_turns_the_wizard_to_French_on_the_same_step()
    {
        await Wizard.Locator("input[data-language='fr']").CheckAsync();

        await Expect(Wizard.Locator("[data-first-run-progress]")).ToHaveTextAsync("Étape 1 sur 5");
        await Expect(Frame).ToHaveAttributeAsync("data-step", "Settings.Language");
        await Expect(Wizard.Locator("[data-first-run-welcome]")).ToContainTextAsync("Quelques choix avant d'être à votre service");
        await Expect(Next).ToHaveTextAsync("Suivant");

        await Next.ClickAsync();
        await Expect(Wizard.Locator("[data-first-run-progress]")).ToHaveTextAsync("Étape 2 sur 5");
        await Expect(Wizard.Locator(".settings-frame-title")).ToHaveTextAsync("Audio");

        await Next.ClickAsync();
        await Expect(Wizard.Locator(".settings-frame-title")).ToHaveTextAsync("Modèles vocaux");
        await Expect(Wizard.Locator("[data-graphics-card]")).ToContainTextAsync("Carte graphique : NVIDIA GeForce RTX 3060, 12 Go");
        await Expect(Row("piper-fr")).ToContainTextAsync("Piper, voix française (Tom)");
        await Expect(Row("whisper-base")).ToContainTextAsync("148 Mo");

        await Next.ClickAsync();
        await Expect(Wizard.Locator(".settings-frame-title")).ToHaveTextAsync("Modèles de langage");
        await Expect(Wizard.Locator("[data-ollama-search]")).ToContainTextAsync("Ollama répond sur le port 11434");

        await Next.ClickAsync();
        await Expect(Wizard.Locator("[data-first-run-done]")).ToContainTextAsync("Dites « Hey Jarvis »");
        await Expect(Wizard.Locator("[data-first-run-finish]")).ToHaveTextAsync("Terminer");

        // Back to the first step, where the teardown puts the language back to English.
        for (var step = 0; step < 4; step++)
        {
            await Wizard.Locator("[data-first-run-back]").ClickAsync();
        }

        await Expect(Wizard.Locator("[data-first-run-progress]")).ToHaveTextAsync("Étape 1 sur 5");
    }

    private ILocator Row(string id) => Wizard.Locator($"[data-model-id='{id}']");
}
