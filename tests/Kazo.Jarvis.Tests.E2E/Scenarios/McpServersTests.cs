using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The tool servers on the Web host (work item stage2/6-mcp-client, ADR 0018, US2.2): a server added by address or
/// by command, the test of its connection with a named cause, its tools all off when they are found, a tool turned
/// on and called, the confirmation before a tool that is not a read, and a tool turned off when its server changes
/// what it says. The servers are simulated in memory: nothing leaves the test machine.
/// </summary>
[TestFixture]
public sealed class McpServersTests : PageTest
{
    private const string Open = "https://mcp.jarvis.example";
    private const string Secure = "https://secure.jarvis.example";

    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Confirmation => Page.Locator("[data-dialog-key='Permissions.Confirmation']");

    private ILocator Newest => Conversation.Locator("[data-conversation-turn]").First;

    private ILocator Calls => Page.Locator("#tool-servers");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
    }

    [TearDown]
    public async Task Restore()
    {
        // The host keeps its settings between the tests: back to no server.
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await OpenServersAsync();
        await Expect(Settings.Locator("[data-mcp-add-name]")).ToBeVisibleAsync();
        var remove = Settings.Locator("[data-mcp-remove]");
        for (var left = await remove.CountAsync(); left > 0; left--)
        {
            await remove.First.ClickAsync();
            await Expect(remove).ToHaveCountAsync(left - 1);
        }

        await CloseDialogsAsync();
    }

    [Test]
    public async Task No_server_at_the_start_and_an_added_server_shows_its_tools_all_off()
    {
        await OpenServersAsync();
        await Expect(Settings.Locator("[data-mcp-none]")).ToBeVisibleAsync();
        await Expect(Settings.Locator("[data-mcp-add-leaves]")).ToBeVisibleAsync();

        // A name that cannot be in the origin of a tool is refused with its reason.
        await AddByAddressAsync("Ma Démo", Open, expectSaved: false);
        await Expect(Settings.Locator("[data-mcp-problem]")).ToBeVisibleAsync();

        await AddByAddressAsync("demo", Open);
        var server = Server("demo");
        await Expect(server).ToHaveAttributeAsync("data-mcp-state", "Connected", new() { Timeout = 10_000 });
        await Expect(server.Locator("[data-mcp-leaves]")).ToContainTextAsync(Open);
        await Expect(server.Locator("[data-mcp-remote]")).ToBeVisibleAsync();
        await Expect(server.Locator("[data-mcp-announced]")).ToContainTextAsync("Demo tools");

        var lookup = server.Locator("[data-mcp-tool='lookup_stock']");
        var send = server.Locator("[data-mcp-tool='send_message']");
        await Expect(lookup).ToHaveAttributeAsync("data-mcp-tool-enabled", "false");
        await Expect(lookup).ToHaveAttributeAsync("data-mcp-tool-class", "Read");
        await Expect(send).ToHaveAttributeAsync("data-mcp-tool-enabled", "false");
        await Expect(send).ToHaveAttributeAsync("data-mcp-tool-class", "Sensitive");
        await Expect(send.Locator(".mcp-tool-description")).ToContainTextAsync("Sends a text message");
        await Expect(server.Locator("[data-mcp-size]")).ToHaveAttributeAsync("data-mcp-tokens", "0");

        // The same tools are on the tools page, with where they come from and what leaves the machine.
        await Settings.Locator("[data-settings-page='Tools.Catalog']").ClickAsync();
        var listed = Settings.Locator("[data-tool='lookup_stock']");
        await Expect(listed).ToHaveAttributeAsync("data-tool-enabled", "false");
        await Expect(listed).ToContainTextAsync("demo");
        await Expect(listed.Locator("[data-tool-discloses]")).ToContainTextAsync(Open);

        // A tool that is off is not offered to the model: no call.
        await TypeAsync("Cours de l'action kazo");
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-tool]")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task A_tool_turned_on_is_called_and_a_tool_that_is_not_a_read_asks_first()
    {
        await OpenServersAsync();
        await AddByAddressAsync("demo", Open);
        var server = Server("demo");
        await TurnOnAsync(server, "lookup_stock");
        await TurnOnAsync(server, "send_message");
        await Expect(server.Locator("[data-mcp-size]")).Not.ToHaveAttributeAsync("data-mcp-tokens", "0");

        await TypeAsync("Cours de l'action kazo");
        await Expect(Newest.Locator("[data-conversation-tool='lookup_stock']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("KAZO : 42,10 $");
        await ExpectIdleAsync();

        // Not declared read-only by its server: a confirmation at each call.
        await TypeAsync("Envoie le message Bonsoir à tous");
        await Expect(Confirmation.Locator("[data-confirmation-window]")).ToHaveAttributeAsync("data-confirmation-class", "Sensitive", new() { Timeout = 10_000 });
        await Expect(Confirmation.Locator("[data-confirmation-tool]")).ToHaveTextAsync("send_message");
        await Expect(Calls).Not.ToHaveAttributeAsync("data-last-call", new Regex("send_message"));
        await Confirmation.Locator("[data-confirmation-approve]").ClickAsync();
        await Expect(Newest.Locator("[data-conversation-tool='send_message']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Calls).ToHaveAttributeAsync("data-last-call", new Regex("send_message.*Bonsoir à tous"));
        await ExpectIdleAsync();
    }

    [Test]
    public async Task The_class_of_a_tool_can_be_raised_and_then_a_read_asks_for_a_click()
    {
        await OpenServersAsync();
        await AddByAddressAsync("demo", Open);
        var server = Server("demo");
        var lookup = server.Locator("[data-mcp-tool='lookup_stock']");
        await TurnOnAsync(server, "lookup_stock");

        await lookup.Locator("[data-mcp-tool-class-choice]").SelectOptionAsync("Critical");
        await Expect(lookup).ToHaveAttributeAsync("data-mcp-tool-class", "Critical");

        // Nothing under what the server declares is offered for a tool that is not a read.
        await Expect(server.Locator("[data-mcp-tool='send_message'] [data-mcp-tool-class-choice] option")).ToHaveCountAsync(2);

        await TypeAsync("Cours de l'action kazo");
        await Expect(Confirmation.Locator("[data-confirmation-window]")).ToHaveAttributeAsync("data-confirmation-class", "Critical", new() { Timeout = 10_000 });
        await Confirmation.Locator("[data-confirmation-refuse]").ClickAsync();
        await Expect(Newest.Locator("[data-conversation-tool='lookup_stock']")).ToHaveAttributeAsync("data-tool-outcome", "Denied", new() { Timeout = 10_000 });
        await ExpectIdleAsync();
    }

    [Test]
    public async Task A_tool_its_server_changed_is_turned_off_until_the_user_sees_it_again()
    {
        await OpenServersAsync();
        await AddByAddressAsync("demo", Open);
        await TurnOnAsync(Server("demo"), "send_message");
        await CloseDialogsAsync();

        // The server now says something else of the tool the user approved.
        await Page.ClickAsync("#revise-tool-servers");
        await OpenServersAsync();
        var server = Server("demo");
        var send = server.Locator("[data-mcp-tool='send_message']");
        await Expect(send).ToHaveAttributeAsync("data-mcp-tool-enabled", "true");
        await server.Locator("[data-mcp-retry]").ClickAsync();

        await Expect(send).ToHaveAttributeAsync("data-mcp-tool-changed", "true", new() { Timeout = 10_000 });
        await Expect(send).ToHaveAttributeAsync("data-mcp-tool-enabled", "false");
        await Expect(send.Locator(".mcp-tool-description")).ToContainTextAsync("reads the contacts");
        await Expect(send.Locator(".mcp-tool-changed")).ToBeVisibleAsync();

        // Read again and turned back on: approved as it is now.
        await TurnOnAsync(server, "send_message");
        await Expect(send).ToHaveAttributeAsync("data-mcp-tool-changed", "false");
    }

    [Test]
    public async Task The_connection_test_names_the_cause_and_the_token_is_kept_in_the_vault()
    {
        await OpenServersAsync();

        // A wrong token: refused, with the cause on the server and in the test.
        await AddByAddressAsync("secure", Secure, token: "mauvais-jeton");
        var secure = Server("secure");
        await Expect(secure).ToHaveAttributeAsync("data-mcp-state", "Failed", new() { Timeout = 10_000 });
        await Expect(secure).ToHaveAttributeAsync("data-mcp-failure", "Rejected");
        await secure.Locator("[data-mcp-test]").ClickAsync();
        await Expect(secure.Locator("[data-mcp-test-result]")).ToHaveAttributeAsync("data-mcp-test-result", "Rejected");
        await Expect(secure.Locator("[data-mcp-tool]")).ToHaveCountAsync(0);

        // The token is a secret of the vault: replaced there, never shown.
        var secret = secure.Locator("[data-secret-ref='mcp.secure.header.Authorization']");
        await Expect(secret).ToHaveAttributeAsync("data-secret-status", "Stored");
        await secret.Locator(".secret-field-input").FillAsync("Bearer bon-jeton");
        await secret.Locator(".secret-field-save").ClickAsync();
        await Expect(secret).ToHaveAttributeAsync("data-secret-status", "Saved");
        await secure.Locator("[data-mcp-retry]").ClickAsync();
        await Expect(secure).ToHaveAttributeAsync("data-mcp-state", "Connected", new() { Timeout = 10_000 });
        await secure.Locator("[data-mcp-test]").ClickAsync();
        await Expect(secure.Locator("[data-mcp-test-result]")).ToHaveAttributeAsync("data-mcp-test-result", "Ok");

        // A server that is down is not a fault of the assistant.
        await AddByAddressAsync("down", "https://down.jarvis.example");
        await Expect(Server("down")).ToHaveAttributeAsync("data-mcp-failure", "Unreachable", new() { Timeout = 10_000 });
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle");
        await Expect(Page.Locator("#fault")).ToHaveAttributeAsync("data-cause", string.Empty);

        // Turned off: not connected, nothing tried.
        await Server("down").Locator("[data-mcp-enabled]").UncheckAsync();
        await Expect(Server("down")).ToHaveAttributeAsync("data-mcp-state", "Disabled");
    }

    [Test]
    public async Task A_server_is_added_by_local_command()
    {
        await OpenServersAsync();

        await AddByCommandAsync("absent", "no-such-program");
        await Expect(Server("absent")).ToHaveAttributeAsync("data-mcp-failure", "CommandNotFound", new() { Timeout = 10_000 });

        await AddByCommandAsync("dice", "demo-server", "--stdio");
        var dice = Server("dice");
        await Expect(dice).ToHaveAttributeAsync("data-mcp-state", "Connected", new() { Timeout = 10_000 });
        await Expect(dice.Locator(".mcp-server-target")).ToHaveTextAsync("demo-server --stdio");
        await Expect(dice.Locator("[data-mcp-remote]")).ToHaveCountAsync(0);
        await TurnOnAsync(dice, "roll_dice");

        await TypeAsync("Lance le dé");
        await Expect(Newest.Locator("[data-conversation-tool='roll_dice']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await ExpectIdleAsync();
    }

    private ILocator Server(string name) => Settings.Locator($"[data-mcp-server='{name}']");

    private async Task OpenServersAsync()
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-settings");
        await Settings.Locator("[data-settings-page='Mcp.Servers']").ClickAsync();
        await Expect(Settings.Locator("[data-mcp-add-name]")).ToBeVisibleAsync();
    }

    private async Task AddByAddressAsync(string name, string address, string? token = null, bool expectSaved = true)
    {
        await Settings.Locator("[data-mcp-add-by-address]").CheckAsync();
        await Settings.Locator("[data-mcp-add-name]").FillAsync(name);
        await Settings.Locator("[data-mcp-add-address]").FillAsync(address);
        await Settings.Locator("[data-mcp-add-token]").FillAsync(token ?? string.Empty);
        await Settings.Locator("[data-mcp-add-button]").ClickAsync();
        if (expectSaved)
        {
            await Expect(Server(name)).ToHaveCountAsync(1);
            await Expect(Settings.Locator("[data-mcp-add-token]")).ToHaveValueAsync(string.Empty);
        }
    }

    private async Task AddByCommandAsync(string name, string command, params string[] arguments)
    {
        await Settings.Locator("[data-mcp-add-by-command]").CheckAsync();
        await Settings.Locator("[data-mcp-add-name]").FillAsync(name);
        await Settings.Locator("[data-mcp-add-command]").FillAsync(command);
        await Settings.Locator("[data-mcp-add-arguments]").FillAsync(string.Join("\n", arguments));
        await Settings.Locator("[data-mcp-add-arguments]").BlurAsync();
        await Settings.Locator("[data-mcp-add-button]").ClickAsync();
        await Expect(Server(name)).ToHaveCountAsync(1);
    }

    private async Task TurnOnAsync(ILocator server, string tool)
    {
        await Expect(server).ToHaveAttributeAsync("data-mcp-state", "Connected", new() { Timeout = 10_000 });
        var entry = server.Locator($"[data-mcp-tool='{tool}']");
        await entry.Locator("[data-mcp-tool-switch]").CheckAsync();
        await Expect(entry).ToHaveAttributeAsync("data-mcp-tool-enabled", "true");
    }

    private async Task TypeAsync(string request)
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-conversation");
        await Expect(Conversation.Locator("[data-conversation-window]")).ToBeVisibleAsync();
        await Conversation.Locator("[data-conversation-text]").FillAsync(request);
        await Conversation.Locator("[data-conversation-text]").PressAsync("Enter");
    }

    private Task ExpectIdleAsync() => Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 10_000 });

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
