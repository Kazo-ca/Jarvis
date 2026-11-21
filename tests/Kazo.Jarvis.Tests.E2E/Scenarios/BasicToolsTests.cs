using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The basic tools on the Web host (work item stage2/5-basic-tools, ADR 0017 section 3, ADR 0022): the tools page
/// with one switch per tool, the file tools that read the allowed folders only, and the web tools, off until the
/// user turns them on, that search and read in the simulated browser of Jarvis. The files are a few in memory and
/// the pages are fixed: nothing leaves the test machine.
/// </summary>
[TestFixture]
public sealed class BasicToolsTests : PageTest
{
    private const string Documents = "/home/demo/Documents";

    private ILocator Root => Page.Locator("#jarvis-root");

    private ILocator Conversation => Page.Locator("[data-dialog-key='Conversation.Window']");

    private ILocator Settings => Page.Locator("[data-dialog-key='Settings.Window']");

    private ILocator Newest => Conversation.Locator("[data-conversation-turn]").First;

    private ILocator Browser => Page.Locator("#browser");

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
        // The host keeps its settings between the tests: back to no allowed folder and the web tools off.
        await CloseDialogsAsync();
        await Expect(Root).ToHaveAttributeAsync("data-phase", "Idle", new() { Timeout = 15_000 });
        await OpenSettingsAsync("Tools.Files");
        await Expect(Settings.Locator("[data-allowed-folder-input]")).ToBeVisibleAsync();
        var remove = Settings.Locator("[data-allowed-folder-remove]");
        for (var left = await remove.CountAsync(); left > 0; left--)
        {
            await remove.First.ClickAsync();
            await Expect(remove).ToHaveCountAsync(left - 1);
        }

        await SwitchAsync("web_search", on: false);
        await SwitchAsync("fetch_url", on: false);
        await CloseDialogsAsync();
    }

    [Test]
    public async Task The_tools_page_lists_every_tool_with_its_switch_and_what_leaves_the_machine()
    {
        await OpenSettingsAsync("Tools.Catalog");

        var clock = Settings.Locator("[data-tool='get_datetime']");
        var search = Settings.Locator("[data-tool='web_search']");
        var fetch = Settings.Locator("[data-tool='fetch_url']");
        await Expect(clock).ToHaveAttributeAsync("data-tool-enabled", "true");
        await Expect(clock.Locator("[data-tool-discloses]")).ToHaveCountAsync(0);
        await Expect(Settings.Locator("[data-tool='read_file']")).ToHaveAttributeAsync("data-tool-enabled", "true");
        await Expect(Settings.Locator("[data-tool='list_directory']")).ToHaveAttributeAsync("data-tool-class", "Read");

        // What makes something leave the machine is off until the user turns it on, and says what leaves.
        await Expect(search).ToHaveAttributeAsync("data-tool-enabled", "false");
        await Expect(fetch).ToHaveAttributeAsync("data-tool-enabled", "false");
        await Expect(search.Locator("[data-tool-discloses]")).ToHaveTextAsync(new Regex("moteur de recherche|search engine"));
        await Expect(fetch.Locator("[data-tool-discloses]")).ToHaveTextAsync(new Regex("adresse de la page|address of the page"));

        // The size of the list given to the model grows with a tool turned on.
        var before = await TokensAsync();
        Assert.That(before, Is.GreaterThan(0));
        await search.Locator("[data-tool-switch]").CheckAsync();
        await Expect(search).ToHaveAttributeAsync("data-tool-enabled", "true");
        await Expect(Settings.Locator("[data-tools-size]")).Not.ToHaveAttributeAsync("data-tools-tokens", before.ToString(CultureInfo.InvariantCulture));
        Assert.That(await TokensAsync(), Is.GreaterThan(before));
    }

    [Test]
    public async Task Without_an_allowed_folder_a_file_cannot_be_read_and_the_refusal_is_logged()
    {
        await TypeAsync($"Lis le fichier {Documents}/notes.txt");

        var call = Newest.Locator("[data-conversation-tool='read_file']");
        await Expect(call).ToHaveAttributeAsync("data-tool-outcome", "Denied", new() { Timeout = 10_000 });
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-answer]")).Not.ToContainTextAsync("farine");

        await ExpectLoggedAsync("read_file", allowed: false, cause: "Forbidden");
    }

    [Test]
    public async Task An_allowed_folder_is_listed_and_read_and_a_link_that_leaves_it_is_refused()
    {
        await OpenSettingsAsync("Tools.Files");
        await Expect(Settings.Locator("[data-allowed-folders-none]")).ToBeVisibleAsync();

        // A relative path is refused at once; a full path is saved and shown.
        await Settings.Locator("[data-allowed-folder-input]").FillAsync("Documents");
        await Settings.Locator("[data-allowed-folder-add]").ClickAsync();
        await Expect(Settings.Locator("[data-allowed-folders-problem]")).ToBeVisibleAsync();
        await Settings.Locator("[data-allowed-folder-input]").FillAsync(Documents);
        await Settings.Locator("[data-allowed-folder-add]").ClickAsync();
        await Expect(Settings.Locator($"[data-allowed-folder='{Documents}']")).ToHaveAttributeAsync("data-folder-usable", "true");
        await CloseDialogsAsync();

        await TypeAsync($"Liste le dossier {Documents}");
        var list = Newest.Locator("[data-conversation-tool='list_directory']");
        await Expect(list).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("recettes");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("notes.txt");
        await ExpectIdleAsync();

        await TypeAsync($"Lis le fichier {Documents}/notes.txt");
        await Expect(Newest.Locator("[data-conversation-tool='read_file']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("farine");
        await ExpectIdleAsync();

        // The link is inside the allowed folder, its target is not: the real place decides.
        await TypeAsync($"Lis le fichier {Documents}/lien-prive/secret.txt");
        await Expect(Newest.Locator("[data-conversation-tool='read_file']")).ToHaveAttributeAsync("data-tool-outcome", "Denied", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-answer]")).Not.ToContainTextAsync("jamais être lu");
        await ExpectIdleAsync();

        await ExpectLoggedAsync("read_file", allowed: false, cause: "Forbidden");
    }

    [Test]
    public async Task The_web_search_is_not_offered_until_it_is_turned_on_then_lists_the_pages_found()
    {
        var searchesBefore = await Browser.GetAttributeAsync("data-searches");

        // Off: the model is not given the tool, so nothing is searched.
        await TypeAsync("Cherche sur le web météo Montréal");
        await Expect(Newest).ToHaveAttributeAsync("data-turn-outcome", "Answered", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-tool]")).ToHaveCountAsync(0);
        await Expect(Browser).ToHaveAttributeAsync("data-searches", searchesBefore ?? "0");
        await ExpectIdleAsync();

        await SwitchAsync("web_search", on: true);
        await CloseDialogsAsync();

        await TypeAsync("Cherche sur le web météo Montréal");
        var call = Newest.Locator("[data-conversation-tool='web_search']");
        await Expect(call).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Browser).ToHaveAttributeAsync("data-last-search", "DuckDuckGo: météo Montréal");
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("pages.jarvis.example/meteo");

        // The pages found are listed in the call, each with « Voir la page », which opens the browser of Jarvis.
        await call.Locator("summary").ClickAsync();
        var pages = call.Locator("[data-conversation-see-page]");
        await Expect(pages).ToHaveCountAsync(3);
        await pages.First.ClickAsync();
        await Expect(Browser).ToHaveAttributeAsync("data-last-shown", "https://pages.jarvis.example/meteo");
    }

    [Test]
    public async Task A_page_is_read_as_data_and_an_address_of_the_machine_is_refused()
    {
        await SwitchAsync("fetch_url", on: true);
        await CloseDialogsAsync();

        await TypeAsync("Lis la page https://pages.jarvis.example/crepes");
        await Expect(Newest.Locator("[data-conversation-tool='fetch_url']")).ToHaveAttributeAsync("data-tool-outcome", "Done", new() { Timeout = 10_000 });
        await Expect(Newest.Locator("[data-conversation-answer]")).ToContainTextAsync("250 g de farine");
        await ExpectIdleAsync();

        // Home Assistant on this machine: denied before anything is asked to the browser.
        await TypeAsync("Lis la page http://127.0.0.1:8123/api/states");
        await Expect(Newest.Locator("[data-conversation-tool='fetch_url']")).ToHaveAttributeAsync("data-tool-outcome", "Denied", new() { Timeout = 10_000 });
        await ExpectIdleAsync();

        await ExpectLoggedAsync("fetch_url", allowed: false, cause: "Forbidden");
    }

    [Test]
    public async Task A_blocked_search_is_told_to_the_model_and_not_worked_around()
    {
        await SwitchAsync("web_search", on: true);
        await CloseDialogsAsync();
        var searchesBefore = int.Parse(await Browser.GetAttributeAsync("data-searches") ?? "0", CultureInfo.InvariantCulture);

        await TypeAsync("Cherche sur le web captcha");

        var call = Newest.Locator("[data-conversation-tool='web_search']");
        await Expect(call).ToHaveAttributeAsync("data-tool-outcome", "Failed", new() { Timeout = 10_000 });
        await call.Locator("summary").ClickAsync();
        await Expect(call.Locator("[data-conversation-tool-result]")).ToContainTextAsync("human");
        await Expect(Browser).ToHaveAttributeAsync("data-searches", (searchesBefore + 1).ToString(CultureInfo.InvariantCulture));
    }

    [Test]
    public async Task The_web_page_says_what_leaves_the_machine_and_drives_the_browser_of_Jarvis()
    {
        await OpenSettingsAsync("Tools.Web");

        await Expect(Settings.Locator("[data-web-tools-leaves]")).ToBeVisibleAsync();
        await Expect(Settings.Locator("[data-web-engine='DuckDuckGo']")).ToBeCheckedAsync();
        await Expect(Settings.Locator($"[data-secret-ref='tools.web.brave.apikey']")).ToHaveCountAsync(1);

        // A local exception with a port is refused with its reason; a host alone is kept.
        await Settings.Locator("[data-web-exceptions]").FillAsync("homeassistant.local:8123");
        await Settings.Locator("[data-web-exceptions]").BlurAsync();
        await Expect(Settings.Locator("[data-web-tools-problem]")).ToBeVisibleAsync();
        await Settings.Locator("[data-web-exceptions]").FillAsync(string.Empty);
        await Settings.Locator("[data-web-exceptions]").BlurAsync();
        await Expect(Settings.Locator("[data-web-tools-problem]")).ToHaveCountAsync(0);

        var shownBefore = int.Parse(await Browser.GetAttributeAsync("data-shown") ?? "0", CultureInfo.InvariantCulture);
        await Settings.Locator("[data-web-browser-open]").ClickAsync();
        await Expect(Browser).ToHaveAttributeAsync("data-shown", (shownBefore + 1).ToString(CultureInfo.InvariantCulture));
        await Settings.Locator("[data-web-browser-clear]").ClickAsync();
        await Expect(Settings.Locator("[data-web-browser-cleared]")).ToBeVisibleAsync();
    }

    private async Task<int> TokensAsync() =>
        int.Parse(await Settings.Locator("[data-tools-size]").GetAttributeAsync("data-tools-tokens") ?? "0", CultureInfo.InvariantCulture);

    private async Task OpenSettingsAsync(string page)
    {
        await CloseDialogsAsync();
        await Page.ClickAsync("#open-settings");
        await Settings.Locator($"[data-settings-page='{page}']").ClickAsync();
    }

    private async Task SwitchAsync(string tool, bool on)
    {
        await OpenSettingsAsync("Tools.Catalog");
        var entry = Settings.Locator($"[data-tool='{tool}']");
        await entry.Locator("[data-tool-switch]").SetCheckedAsync(on);
        await Expect(entry).ToHaveAttributeAsync("data-tool-enabled", on ? "true" : "false");
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

    /// <summary>The newest decision of the permission log is the one described.</summary>
    private async Task ExpectLoggedAsync(string tool, bool allowed, string cause)
    {
        await OpenSettingsAsync("Permissions.Decision");
        var newest = Settings.Locator("[data-decision-log] > li").First;
        await Expect(newest).ToHaveAttributeAsync("data-decision-tool", tool);
        await Expect(newest).ToHaveAttributeAsync("data-decision-allowed", allowed ? "true" : "false");
        await Expect(newest).ToHaveAttributeAsync("data-decision-cause", cause);
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
