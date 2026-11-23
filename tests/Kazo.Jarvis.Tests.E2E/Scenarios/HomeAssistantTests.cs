using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// Home Assistant on the Web host (work item stage2/7-home-assistant, ADR 0018 section 5, US2.2): with the same
/// tool, a light is turned on without a question and a lock asks for a click; a script runs without a question
/// until the user marks it « sécurité »; an entity the user marks asks for a click too. The Home Assistant is
/// simulated in memory: nothing leaves the test machine and no device exists.
/// </summary>
[TestFixture]
public sealed class HomeAssistantTests : PageTest
{
    private const string Home = "https://maison.jarvis.example";

    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Confirmation => Page.Locator("[data-dialog-key='Permissions.Confirmation']");

    private ILocator Newest => Conversation.Locator("[data-conversation-turn]").First;

    private ILocator Calls => Page.Locator("#tool-servers");

    private ILocator Server => Settings.Locator("[data-mcp-server='maison']");

    [SetUp]
    public async Task OpenHome()
    {
        await Page.GotoAsync(WebHost.BaseAddress.ToString());
        await Expect(Root).ToHaveAttributeAsync("data-interactive", "true");
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });

        await OpenPageAsync("Mcp.Servers");
        await Settings.Locator("[data-mcp-add-by-address]").CheckAsync();
        await Settings.Locator("[data-mcp-add-name]").FillAsync("maison");
        await Settings.Locator("[data-mcp-add-address]").FillAsync(Home);
        await Settings.Locator("[data-mcp-add-kind]").SelectOptionAsync("HomeAssistant");
        await Settings.Locator("[data-mcp-add-button]").ClickAsync();
        await Expect(Server).ToHaveAttributeAsync("data-mcp-state", "Connected", new() { Timeout = 10_000 });
    }

    [TearDown]
    public async Task Restore()
    {
        // The host keeps its settings between the tests: back to no mark and no server.
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await OpenPageAsync("Mcp.HomeAssistant");
        var entities = Settings.Locator("[data-ha-security-entities]");
        await entities.FillAsync(string.Empty);
        await entities.BlurAsync();
        var marked = Settings.Locator("[data-ha-script-security='true'] [data-ha-script-mark]");
        for (var left = await marked.CountAsync(); left > 0; left--)
        {
            await marked.First.UncheckAsync();
            await Expect(marked).ToHaveCountAsync(left - 1);
        }

        await Settings.Locator("[data-settings-page='Mcp.Servers']").ClickAsync();
        var remove = Settings.Locator("[data-mcp-remove]");
        await Expect(remove.First).ToBeVisibleAsync();
        for (var left = await remove.CountAsync(); left > 0; left--)
        {
            await remove.First.ClickAsync();
            await Expect(remove).ToHaveCountAsync(left - 1);
        }

        await CloseDialogsAsync();
    }

    [Test]
    public async Task A_light_is_turned_on_without_a_question_and_a_lock_asks_for_a_click()
    {
        // The tools are off when they are found, classed by what Home Assistant is, not by the general rule.
        var turnOn = Server.Locator("[data-mcp-tool='intent__HassTurnOn']");
        await Expect(turnOn).ToHaveAttributeAsync("data-mcp-tool-enabled", "false");
        await Expect(turnOn).ToHaveAttributeAsync("data-mcp-tool-class", "EverydayEffect");
        await Expect(Server.Locator("[data-mcp-tool='homeassistant__GetLiveContext']")).ToHaveAttributeAsync("data-mcp-tool-class", "Read");
        await Expect(Server.Locator("[data-mcp-tool='script__depart'] [data-mcp-script-warning]")).ToBeVisibleAsync();
        await Expect(turnOn.Locator("[data-mcp-script-warning]")).ToHaveCountAsync(0);

        await TurnOnAsync("intent__HassTurnOn");
        await TurnOnAsync("intent__HassTurnOff");

        await TypeAsync("Allume Lampe du salon");
        await Expect(Newest.Locator("[data-conversation-tool='intent__HassTurnOn']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Calls).ToHaveAttributeAsync("data-last-call", new Regex("intent__HassTurnOn.*Lampe du salon"));
        await Expect(Confirmation).ToHaveCountAsync(0);
        await ExpectIdleAsync();

        // The same tool that turns a light off unlocks a lock: a click on this computer, and nothing done before it.
        await TypeAsync("Déverrouille Serrure de l'entrée");
        await Expect(Confirmation.Locator("[data-confirmation-window]")).ToHaveAttributeAsync("data-confirmation-class", "Critical", new() { Timeout = 10_000 });
        await Expect(Confirmation.Locator("[data-confirmation-tool]")).ToHaveTextAsync("intent__HassTurnOff");
        await Expect(Calls).Not.ToHaveAttributeAsync("data-last-call", new Regex("Serrure"));
        await Confirmation.Locator("[data-confirmation-refuse]").ClickAsync();
        await Expect(Newest.Locator("[data-conversation-tool='intent__HassTurnOff']")).ToHaveAttributeAsync("data-tool-outcome", "Denied", new() { Timeout = 10_000 });
        await Expect(Calls).Not.ToHaveAttributeAsync("data-last-call", new Regex("Serrure"));
        await ExpectIdleAsync();

        // A room that holds the lock is classed like the lock; a room of lights is not.
        await TypeAsync("Éteins tout dans la pièce Entrée");
        await Expect(Confirmation.Locator("[data-confirmation-window]")).ToHaveAttributeAsync("data-confirmation-class", "Critical", new() { Timeout = 10_000 });
        await Confirmation.Locator("[data-confirmation-refuse]").ClickAsync();
        await ExpectIdleAsync();
    }

    [Test]
    public async Task A_script_runs_without_a_question_until_the_user_marks_it()
    {
        await TurnOnAsync("script__depart");

        await TypeAsync("Lance le script depart");
        await Expect(Newest.Locator("[data-conversation-tool='script__depart']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Confirmation).ToHaveCountAsync(0);
        await ExpectIdleAsync();

        await OpenPageAsync("Mcp.HomeAssistant");
        await Expect(Settings.Locator("[data-ha-always]")).ToBeVisibleAsync();
        var script = Settings.Locator("[data-ha-script='script__depart']");
        await Expect(script).ToHaveAttributeAsync("data-ha-script-security", "false");
        await Expect(Settings.Locator("[data-ha-script]")).ToHaveCountAsync(2);
        await script.Locator("[data-ha-script-mark]").CheckAsync();
        await Expect(script).ToHaveAttributeAsync("data-ha-script-security", "true");

        await TypeAsync("Lance le script depart");
        await Expect(Confirmation.Locator("[data-confirmation-window]")).ToHaveAttributeAsync("data-confirmation-class", "Critical", new() { Timeout = 10_000 });
        await Confirmation.Locator("[data-confirmation-approve]").ClickAsync();
        await Expect(Newest.Locator("[data-conversation-tool='script__depart']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await ExpectIdleAsync();
    }

    [Test]
    public async Task An_entity_the_user_marks_asks_for_a_click()
    {
        await TurnOnAsync("intent__HassTurnOn");

        await TypeAsync("Allume Prise du sapin");
        await Expect(Newest.Locator("[data-conversation-tool='intent__HassTurnOn']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await ExpectIdleAsync();

        await OpenPageAsync("Mcp.HomeAssistant");
        var entities = Settings.Locator("[data-ha-security-entities]");
        await entities.FillAsync("Prise du sapin");
        await entities.BlurAsync();
        await Expect(Settings.Locator("[data-ha-problem]")).ToHaveCountAsync(0);

        await TypeAsync("Allume Prise du sapin");
        await Expect(Confirmation.Locator("[data-confirmation-window]")).ToHaveAttributeAsync("data-confirmation-class", "Critical", new() { Timeout = 10_000 });
        await Confirmation.Locator("[data-confirmation-refuse]").ClickAsync();
        await Expect(Newest.Locator("[data-conversation-tool='intent__HassTurnOn']")).ToHaveAttributeAsync("data-tool-outcome", "Denied", new() { Timeout = 10_000 });
        await ExpectIdleAsync();
    }

    private async Task OpenPageAsync(string page)
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-settings");
        await Settings.Locator($"[data-settings-page='{page}']").ClickAsync();
    }

    private async Task TurnOnAsync(string tool)
    {
        var entry = Server.Locator($"[data-mcp-tool='{tool}']");
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
