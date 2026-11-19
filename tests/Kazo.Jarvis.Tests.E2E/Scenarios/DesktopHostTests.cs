using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The desktop contracts on the Web host (work item stage1/4-hosts): the button that simulates the global
/// hotkey reaches the state machine, the orb shows and hides, dialogs open as layers, one per key.
/// </summary>
[TestFixture]
public sealed class DesktopHostTests : PageTest
{
    private ILocator Root => Page.Locator("#jarvis-root");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());

        // The page is prerendered: the buttons answer only once the interactive circuit is up.
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await Expect(Page.Locator("#simulate-hotkey")).ToContainTextAsync("Ctrl+Alt+J");
    }

    [Test]
    public async Task The_simulated_hotkey_starts_listening_then_cancels_it()
    {
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");

        await Page.ClickAsync("#simulate-hotkey");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Listening");

        await Page.ClickAsync("#simulate-hotkey");
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
    }

    [Test]
    public async Task The_orb_hides_and_shows_again()
    {
        var orb = Page.Locator(".orb-disc");
        await Expect(orb).ToBeVisibleAsync();

        await Page.ClickAsync("#toggle-orb");
        await Expect(orb).ToBeHiddenAsync();

        await Page.ClickAsync("#toggle-orb");
        await Expect(orb).ToBeVisibleAsync();
    }

    [Test]
    public async Task A_dialog_opens_over_the_page_once_per_key_and_closes()
    {
        var dialogs = Page.Locator("[data-dialog-key='Web.Sample']");

        await Page.ClickAsync("#open-sample-dialog");
        await Expect(dialogs).ToHaveCountAsync(1);
        await Expect(dialogs.Locator(".sample-dialog")).ToHaveTextAsync("Opened through the dialog contract.");
        await Expect(Page.GetByRole(AriaRole.Dialog, new() { Name = "Sample dialog" })).ToBeVisibleAsync();

        await Page.ClickAsync("#open-sample-dialog");
        await Expect(dialogs).ToHaveCountAsync(1);

        await dialogs.Locator(".dialog-close").ClickAsync();
        await Expect(dialogs).ToHaveCountAsync(0);
    }
}
