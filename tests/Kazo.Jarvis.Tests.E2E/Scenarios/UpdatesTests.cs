using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The updates window on the Web host (work item stage1/16-updates, US1.18), with simulated releases and a
/// simulated updater. The host is shared by every test, so the whole journey is one test.
/// </summary>
[TestFixture]
public sealed class UpdatesTests : PageTest
{
    private ILocator Panel => Page.Locator("#updates-panel");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Page.Locator("#jarvis-root")).ToHaveAttributeAsync("data-interactive", "true");
    }

    [Test]
    public async Task Check_fails_offline_then_finds_and_installs_a_new_version()
    {
        await Page.ClickAsync("#open-updates");
        await Expect(Page.GetByRole(AriaRole.Dialog, new() { Name = "Updates" })).ToBeVisibleAsync();
        await Expect(Page.Locator("#updates-current")).ToContainTextAsync("Installed version: 0.0.1");
        await Expect(Page.Locator("#updates-automatic")).Not.ToBeCheckedAsync();

        // No network: the failure is named, nothing to install.
        await Page.ClickAsync("#simulate-offline");
        await Page.ClickAsync("#updates-check");
        await Expect(Panel).ToHaveAttributeAsync("data-phase", "Failed");
        await Expect(Page.Locator("#updates-message")).ToContainTextAsync("GitHub could not be reached");
        await Expect(Page.Locator("#updates-install")).ToHaveCountAsync(0);

        // Back online, nothing newer published yet.
        await Page.ClickAsync("#simulate-offline");
        await Page.ClickAsync("#updates-check");
        await Expect(Panel).ToHaveAttributeAsync("data-phase", "UpToDate");

        // A new version is published: offered, with its release notes.
        await Page.ClickAsync("#simulate-release");
        await Page.ClickAsync("#updates-check");
        await Expect(Panel).ToHaveAttributeAsync("data-phase", "Available");
        await Expect(Page.Locator("#updates-message")).ToHaveTextAsync("Version 9.9.0 is available.");
        await Expect(Page.Locator("#updates-notes")).ToHaveAttributeAsync("href", "https://github.com/Kazo-ca/Jarvis/releases/tag/v9.9.0");

        // Install: the download shows its progress, then the updater is started.
        await Page.ClickAsync("#updates-install");
        await Expect(Page.Locator("#updates-progress")).ToBeVisibleAsync();
        await Expect(Panel).ToHaveAttributeAsync("data-phase", "Installing");
        await Expect(Page.Locator("#updates-message")).ToContainTextAsync("Jarvis closes to install version 9.9.0");
        await Expect(Page.Locator("#updater-launched")).ToHaveAttributeAsync("data-folder", "simulated/updates/Kazo.Jarvis-9.9.0-win-x64/content");
    }

    [Test]
    public async Task The_automatic_check_is_a_setting_kept_across_windows()
    {
        await Page.ClickAsync("#open-updates");
        await Page.Locator("#updates-automatic").CheckAsync();
        await Page.Locator("[data-dialog-key='Updates.Panel'] .dialog-close").ClickAsync();

        await Page.ClickAsync("#open-updates");
        await Expect(Page.Locator("#updates-automatic")).ToBeCheckedAsync();

        // Leave the shared host as found.
        await Page.Locator("#updates-automatic").UncheckAsync();
    }
}
