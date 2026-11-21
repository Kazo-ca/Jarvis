using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The "Claude Code hooks" page on the Web host (work item stage2/11-hook-server, ADR 0021, sections 3 and 6,
/// US2.12): the local server and its port, the exact lines shown before anything is written, an installation
/// that keeps the user's own settings and makes a copy, a removal of the lines of Jarvis only, and a file that
/// is not plain JSON left untouched. The user's settings file and the server are simulated.
/// </summary>
[TestFixture]
public sealed class ClaudeHooksTests : PageTest
{
    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Hooks => Settings.Locator("[data-hooks]");

    private ILocator File => Page.Locator("#claude-settings");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await CloseDialogsAsync();
        await ResetAsync();
    }

    [TearDown]
    public async Task Restore()
    {
        await CloseDialogsAsync();
        await ResetAsync();
    }

    [Test]
    public async Task The_page_shows_the_server_and_the_exact_lines_before_anything_is_written()
    {
        await OpenPageAsync();

        await Expect(Settings.Locator(".settings-frame-title")).ToHaveTextAsync("Claude Code hooks");
        await Expect(Settings.Locator("[data-hooks-server]")).ToHaveAttributeAsync("data-hooks-listening", "true");
        await Expect(Settings.Locator("[data-hooks-server]")).ToHaveTextAsync("Listening on 127.0.0.1, port 47821.");
        await Expect(Settings.Locator("[data-hooks-server-notes]")).ToContainTextAsync("never on the network");
        await Expect(Settings.Locator("[data-hooks-server-notes]")).ToContainTextAsync("No request is accepted without a token.");
        await Expect(Hooks).ToHaveAttributeAsync("data-hooks-install-state", "None");
        await Expect(Hooks.Locator("[data-hooks-state-text]")).ToHaveTextAsync("Not installed.");
        await Expect(Hooks.Locator("[data-hooks-file]")).ToContainTextAsync(@"C:\Users\test\.claude\settings.json");
        await Expect(Hooks.Locator("[data-hooks-notes] li")).ToHaveCountAsync(4);
        await Expect(Hooks.Locator("[data-hooks-notes]")).ToContainTextAsync("Nothing is written to your file before you click");

        var preview = Hooks.Locator("[data-hooks-preview]");
        await Expect(preview).ToContainTextAsync("\"SessionStart\"");
        await Expect(preview).ToContainTextAsync("\"async\": true");
        await Expect(preview).ToContainTextAsync("http://127.0.0.1:47821/hook/task.start");
        await Expect(preview).ToContainTextAsync("http://127.0.0.1:47821/hook/approval.requested");
        await Expect(Hooks.Locator("[data-hooks-install]")).ToHaveTextAsync("Install");
        await Expect(Hooks.Locator("[data-hooks-remove]")).ToHaveCountAsync(0);

        // Looking wrote nothing.
        await Expect(File).ToHaveAttributeAsync("data-copies", "0");
        await Expect(File).ToHaveAttributeAsync("data-jarvis-hooks", "false");
    }

    [Test]
    public async Task Installing_keeps_the_users_settings_makes_a_copy_and_removing_takes_only_the_lines_of_Jarvis()
    {
        await OpenPageAsync();

        await Hooks.Locator("[data-hooks-install]").ClickAsync();

        await Expect(Hooks).ToHaveAttributeAsync("data-hooks-install-state", "Installed");
        await Expect(Hooks.Locator("[data-hooks-result]")).ToHaveAttributeAsync("data-hooks-result", "Done");
        await Expect(Hooks.Locator("[data-hooks-result]")).ToContainTextAsync("Hooks installed.");
        await Expect(Hooks.Locator("[data-hooks-result]")).ToContainTextAsync(".bak");
        await Expect(Hooks.Locator("[data-hooks-install]")).ToHaveTextAsync("Install again");
        await Expect(File).ToHaveAttributeAsync("data-copies", "1");
        await Expect(File).ToHaveAttributeAsync("data-jarvis-hooks", "true");
        await Expect(File).ToHaveAttributeAsync("data-kept", "true");

        await Hooks.Locator("[data-hooks-remove]").ClickAsync();

        await Expect(Hooks).ToHaveAttributeAsync("data-hooks-install-state", "None");
        await Expect(Hooks.Locator("[data-hooks-result]")).ToContainTextAsync("Hooks removed.");
        await Expect(File).ToHaveAttributeAsync("data-copies", "2");
        await Expect(File).ToHaveAttributeAsync("data-jarvis-hooks", "false");
        await Expect(File).ToHaveAttributeAsync("data-kept", "true");
    }

    [Test]
    public async Task A_new_port_makes_the_installed_hooks_outdated_until_they_are_installed_again()
    {
        await OpenPageAsync();
        await Hooks.Locator("[data-hooks-install]").ClickAsync();
        await Expect(Hooks).ToHaveAttributeAsync("data-hooks-install-state", "Installed");

        var port = Settings.Locator("[data-hooks-port]");
        await port.FillAsync("80");
        await port.PressAsync("Tab");
        await Expect(Settings.Locator("[data-hooks-problem]")).ToContainTextAsync("1024");
        await Expect(Hooks).ToHaveAttributeAsync("data-hooks-install-state", "Installed");

        await port.FillAsync("50123");
        await port.PressAsync("Tab");

        await Expect(Settings.Locator("[data-hooks-problem]")).ToHaveCountAsync(0);
        await Expect(Settings.Locator("[data-hooks-server]")).ToHaveAttributeAsync("data-hooks-server-port", "50123");
        await Expect(Hooks).ToHaveAttributeAsync("data-hooks-install-state", "Outdated");
        await Expect(Hooks.Locator("[data-hooks-preview]")).ToContainTextAsync("http://127.0.0.1:50123/hook/task.start");

        await Hooks.Locator("[data-hooks-install]").ClickAsync();

        await Expect(Hooks).ToHaveAttributeAsync("data-hooks-install-state", "Installed");
        await Expect(File).ToHaveAttributeAsync("data-copies", "2");
    }

    [Test]
    public async Task A_file_that_is_not_plain_JSON_is_left_alone_and_the_lines_are_given_to_paste()
    {
        await Page.ClickAsync("#break-claude-settings");
        await OpenPageAsync();

        await Expect(Hooks).ToHaveAttributeAsync("data-hooks-install-state", "Unreadable");
        await Expect(Hooks.Locator("[data-hooks-state-text]")).ToContainTextAsync("it will not change it");
        await Expect(Hooks).ToContainTextAsync("The lines to add by hand:");
        await Expect(Hooks.Locator("[data-hooks-preview]")).ToContainTextAsync("/hook/task.idle");
        await Expect(Hooks.Locator("[data-hooks-install]")).ToHaveCountAsync(0);
        await Expect(Hooks.Locator("[data-hooks-remove]")).ToHaveCountAsync(0);
        await Expect(File).ToHaveAttributeAsync("data-copies", "0");
    }

    private async Task OpenPageAsync()
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-settings");
        await Settings.Locator("[data-settings-page='ClaudeCode.Hooks']").ClickAsync();
        await Expect(Hooks).ToBeVisibleAsync();
    }

    /// <summary>The default port, no hook of Jarvis and the file as at the start: the host keeps them between the tests.</summary>
    private async Task ResetAsync()
    {
        await Page.ClickAsync("#reset-claude-settings");
        await OpenPageAsync();
        var port = Settings.Locator("[data-hooks-port]");
        if (await port.InputValueAsync() != "47821")
        {
            await port.FillAsync("47821");
            await port.PressAsync("Tab");
            await Expect(Settings.Locator("[data-hooks-server]")).ToHaveAttributeAsync("data-hooks-server-port", "47821");
        }

        await CloseDialogsAsync();
        await Expect(File).ToHaveAttributeAsync("data-copies", "0");
    }

    private async Task CloseDialogsAsync()
    {
        // The dialogs live in the host, not in the page: a window left open would cover the buttons of the next test.
        if (await Settings.CountAsync() == 1)
        {
            // The settings window remembers its page; the next test starts on the first one.
            await Settings.Locator("[data-settings-page='Settings.Language']").ClickAsync();
        }

        var dialogs = Page.Locator(".dialog-layer");
        for (var open = await dialogs.CountAsync(); open > 0; open--)
        {
            await dialogs.Last.Locator(".dialog-close").ClickAsync();
            await Expect(dialogs).ToHaveCountAsync(open - 1);
        }
    }
}
