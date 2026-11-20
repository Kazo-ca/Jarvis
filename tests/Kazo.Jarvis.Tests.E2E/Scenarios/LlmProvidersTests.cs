using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The providers page (US1.8, ADR 0007, section 1) in the Web host, against the simulated connector: the test
/// button names each cause of failure. Nothing is saved, so the other tests keep the default settings.
/// </summary>
[TestFixture]
public sealed class LlmProvidersTests : PageTest
{
    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Local => Settings.Locator("[data-provider-id='local']");

    [SetUp]
    public async Task OpenProviders()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Page.Locator("#jarvis-root")).ToHaveAttributeAsync("data-interactive", "true");
        await Page.ClickAsync("#open-settings");
        await Settings.Locator("[data-settings-page='Llm.Providers']").ClickAsync();
        await Expect(Settings.Locator(".settings-frame-title")).ToHaveTextAsync("Language models");
    }

    [TearDown]
    public async Task Close() => await Page.ClickAsync("[data-dialog-key='Settings.Window'] .dialog-close");

    [Test]
    public async Task The_default_provider_is_local_ollama_with_qwen3()
    {
        await Expect(Local.Locator("[data-provider-default]")).ToBeCheckedAsync();
        await Expect(Local.Locator("[data-field='model']")).ToHaveValueAsync("qwen3:14b");
        await Expect(Local.Locator("[data-field='address']")).ToHaveValueAsync("http://127.0.0.1:11434");
        await Expect(Settings.Locator("[data-providers-save]")).ToBeDisabledAsync();
    }

    [Test]
    public async Task The_test_passes_then_names_a_missing_model_and_an_unreachable_server()
    {
        var result = Local.Locator("[data-test-result]");

        await Local.Locator("[data-provider-test]").ClickAsync();
        await Expect(result).ToHaveAttributeAsync("data-test-result", "Passed");
        await Expect(result).ToContainTextAsync("Model “qwen3:14b” is available");

        await Local.Locator("[data-field='model']").FillAsync("llama9");
        await Local.Locator("[data-field='model']").BlurAsync();
        await Expect(result).ToHaveCountAsync(0);
        await Local.Locator("[data-provider-test]").ClickAsync();
        await Expect(result).ToHaveAttributeAsync("data-test-result", "ModelMissing");
        await Expect(result).ToContainTextAsync("ollama pull llama9");
        await Expect(result).ToContainTextAsync("qwen2.5:14b");

        await Local.Locator("[data-field='address']").FillAsync("http://ollama.invalid:11435");
        await Local.Locator("[data-field='address']").BlurAsync();
        await Local.Locator("[data-provider-test]").ClickAsync();
        await Expect(result).ToHaveAttributeAsync("data-test-result", "Unreachable");
        await Expect(result).ToContainTextAsync("check the address and the port");

        await Expect(Settings.Locator("[data-providers-save]")).ToBeEnabledAsync();
    }

    [Test]
    public async Task A_cloud_provider_is_tested_but_cannot_be_the_default()
    {
        await Settings.Locator("[data-field='add-kind']").SelectOptionAsync("Anthropic");
        await Settings.Locator("[data-provider-add]").ClickAsync();
        var claude = Settings.Locator("[data-provider-id='anthropic']");

        await Expect(claude.Locator("[data-field='model']")).ToHaveValueAsync("claude-opus-5-5");
        await Expect(claude.Locator("[data-provider-default]")).ToBeDisabledAsync();
        await claude.Locator("[data-provider-test]").ClickAsync();
        await Expect(claude.Locator("[data-test-result]")).ToHaveAttributeAsync("data-test-result", "KeyMissing");

        await claude.Locator(".secret-field-input").FillAsync("sk-ant-simulated");
        await claude.Locator(".secret-field-save").ClickAsync();
        await claude.Locator("[data-provider-test]").ClickAsync();
        await Expect(claude.Locator("[data-test-result]")).ToHaveAttributeAsync("data-test-result", "Passed");

        // Leave the vault as it was for the other tests.
        await claude.Locator(".secret-field-delete").ClickAsync();
    }
}
