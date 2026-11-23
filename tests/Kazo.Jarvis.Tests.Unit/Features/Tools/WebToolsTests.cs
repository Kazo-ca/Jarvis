using System.Net;
using System.Text.Json;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Tools.Web;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Kernel.Web;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Tests.Unit.Adapters.Secrets;

namespace Kazo.Jarvis.Tests.Unit.Features.Tools;

/// <summary>
/// The web tools (ADR 0017, section 3; ADR 0022): off by default, read only, never this machine nor the local
/// network, Brave Search as soon as a key is stored and the browser of Jarvis otherwise.
/// </summary>
[TestFixture]
public sealed class WebToolsTests
{
    private TemporaryFolder folder = null!;
    private ISettingsStore settings = null!;
    private WebAddressPolicy policy = null!;
    private FakeBrowser browser = null!;
    private FakeSearchApi api = null!;
    private FakeVault vault = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        policy = new WebAddressPolicy(settings);
        browser = new FakeBrowser();
        api = new FakeSearchApi();
        vault = new FakeVault();
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    private static JsonElement Arguments(object value) => JsonSerializer.SerializeToElement(value);

    private FetchUrlTool Fetch(bool withBrowser = true) => new(settings, policy, withBrowser ? browser : null);

    private WebSearchTool Search(bool withBrowser = true, bool withApi = true) => new(settings, vault, withApi ? api : null, withBrowser ? browser : null);

    [Test]
    public void Both_tools_are_reads_off_by_default_and_say_what_leaves_the_machine()
    {
        foreach (ITool tool in new ITool[] { Fetch(), Search() })
        {
            Assert.Multiple(() =>
            {
                Assert.That(tool.Name, Does.Match("^[a-z][a-z0-9_]*$"));
                Assert.That(tool.Origin, Is.EqualTo(ToolOrigins.BuiltIn));
                Assert.That(tool.DeclaredClass, Is.EqualTo(ActionClass.Read));
                Assert.That(tool.EnabledByDefault, Is.False, "Something leaves the machine (ADR 0017, section 6).");
                Assert.That(tool.Discloses(AssistantLanguage.French), Is.Not.Empty.And.Not.EqualTo(tool.Discloses(AssistantLanguage.English)));
                Assert.That(tool.Describe(AssistantLanguage.French), Is.Not.Empty.And.Not.EqualTo(tool.Describe(AssistantLanguage.English)));
            });
        }
    }

    [TestCase("https://example.com/page", true)]
    [TestCase("http://example.com", true)]
    [TestCase("https://8.8.8.8/", true)]
    [TestCase("https://[2606:4700:4700::1111]/", true)]
    [TestCase("ftp://example.com/file", false)]
    [TestCase("file:///C:/Windows/win.ini", false)]
    [TestCase("javascript:alert(1)", false)]
    [TestCase("https://user:secret@example.com/", false)]
    [TestCase("http://localhost:8123/", false)]
    [TestCase("http://LOCALHOST./", false)]
    [TestCase("http://app.localhost/", false)]
    [TestCase("http://homeassistant.local:8123/", false)]
    [TestCase("http://router/", false)]
    [TestCase("http://nas.lan/", false)]
    [TestCase("http://127.0.0.1:11435/", false)]
    [TestCase("http://127.1/", false)]
    [TestCase("http://2130706433/", false)]
    [TestCase("http://0.0.0.0/", false)]
    [TestCase("http://10.0.0.5/", false)]
    [TestCase("http://172.16.4.1/", false)]
    [TestCase("http://172.32.0.1/", true)]
    [TestCase("http://192.168.1.1/", false)]
    [TestCase("http://169.254.169.254/latest/meta-data", false)]
    [TestCase("http://100.100.1.1/", false)]
    [TestCase("http://[::1]/", false)]
    [TestCase("http://[::ffff:192.168.1.1]/", false)]
    [TestCase("http://[fe80::1]/", false)]
    [TestCase("http://[fd12:3456::1]/", false)]
    public void Only_public_http_addresses_may_be_read(string address, bool allowed) =>
        Assert.That(policy.Allows(new Uri(address)), Is.EqualTo(allowed));

    [TestCase("127.0.0.1", false)]
    [TestCase("192.168.0.10", false)]
    [TestCase("10.1.2.3", false)]
    [TestCase("::1", false)]
    [TestCase("fe80::abcd", false)]
    [TestCase("93.184.216.34", true)]
    [TestCase("2606:2800:220:1::1", true)]
    public void A_public_name_that_resolves_to_a_local_address_is_refused(string resolved, bool allowed) =>
        Assert.That(policy.AllowsResolved("innocent.example.com", IPAddress.Parse(resolved)), Is.EqualTo(allowed));

    [Test]
    public async Task A_host_the_user_listed_may_be_read_all_the_same()
    {
        await settings.WriteAsync(WebToolsOptions.Section, new WebToolsOptions { LocalExceptions = ["homeassistant.local", "192.168.1.20"] });

        Assert.Multiple(() =>
        {
            Assert.That(policy.Allows(new Uri("http://homeassistant.local:8123/")), Is.True);
            Assert.That(policy.Allows(new Uri("http://HomeAssistant.Local/")), Is.True);
            Assert.That(policy.Allows(new Uri("http://192.168.1.20/")), Is.True);
            Assert.That(policy.AllowsResolved("homeassistant.local", IPAddress.Parse("192.168.1.20")), Is.True);
            Assert.That(policy.Allows(new Uri("http://192.168.1.21/")), Is.False, "Only what is listed.");
            Assert.That(policy.Allows(new Uri("file://homeassistant.local/c$")), Is.False, "An exception does not change the scheme.");
        });
    }

    [TestCase("homeassistant.local:8123")]
    [TestCase("http://homeassistant.local")]
    [TestCase("a b")]
    public async Task A_local_exception_is_a_host_alone(string exception)
    {
        var result = await settings.WriteAsync(WebToolsOptions.Section, new WebToolsOptions { LocalExceptions = [exception] });

        Assert.That(result.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
    }

    [Test]
    public void A_local_address_is_forbidden_and_a_public_one_is_a_read()
    {
        var fetch = Fetch();

        Assert.Multiple(() =>
        {
            Assert.That(fetch.Classify(Arguments(new { url = "https://example.com/a" })), Is.EqualTo(ActionClass.Read));
            Assert.That(fetch.Classify(Arguments(new { url = "example.com/a" })), Is.EqualTo(ActionClass.Read), "Read as https://example.com/a.");
            Assert.That(fetch.Classify(Arguments(new { url = "http://127.0.0.1:8123/api/states" })), Is.EqualTo(ActionClass.Forbidden));
            Assert.That(fetch.Classify(Arguments(new { url = "localhost:5000" })), Is.EqualTo(ActionClass.Forbidden));
            Assert.That(fetch.Classify(Arguments(new { url = "file:///C:/secret.txt" })), Is.EqualTo(ActionClass.Forbidden));
        });
    }

    [Test]
    public async Task A_page_is_returned_as_data_with_its_final_address()
    {
        browser.Pages["https://example.com/a"] = new WebPage(new Uri("https://www.example.com/a"), "Un titre", "Le texte « utile » de la page.");

        using var result = JsonDocument.Parse(await Fetch().InvokeAsync(Arguments(new { url = "https://example.com/a" }), CancellationToken.None));

        var root = result.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("url").GetString(), Is.EqualTo("https://www.example.com/a"), "After the redirection.");
            Assert.That(root.GetProperty("title").GetString(), Is.EqualTo("Un titre"));
            Assert.That(root.GetProperty("text").GetString(), Is.EqualTo("Le texte « utile » de la page."));
            Assert.That(root.TryGetProperty("next_start", out _), Is.False);
        });
    }

    [Test]
    public async Task A_long_page_is_read_by_parts()
    {
        var text = string.Concat(Enumerable.Range(1, 400).Select(line => $"Paragraphe {line} : \"texte\".\n"));
        browser.Pages["https://example.com/long"] = new WebPage(new Uri("https://example.com/long"), "Long", text);
        var fetch = Fetch();

        var first = await fetch.InvokeAsync(Arguments(new { url = "https://example.com/long" }), CancellationToken.None);
        using var firstPart = JsonDocument.Parse(first);
        var next = firstPart.RootElement.GetProperty("next_start").GetInt32();
        using var secondPart = JsonDocument.Parse(await fetch.InvokeAsync(Arguments(new { url = "https://example.com/long", start = next }), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(first, Has.Length.LessThanOrEqualTo(WebToolsOptions.MostResultCharacters));
            Assert.That(firstPart.RootElement.GetProperty("text").GetString(), Is.EqualTo(text[..next]));
            Assert.That(firstPart.RootElement.GetProperty("total_characters").GetInt32(), Is.EqualTo(text.Length));
            Assert.That(secondPart.RootElement.GetProperty("text").GetString(), Does.StartWith(text.Substring(next, 20)));
        });
    }

    [Test]
    public void A_local_address_is_never_asked_to_the_browser_even_if_the_call_runs()
    {
        Assert.That(async () => await Fetch().InvokeAsync(Arguments(new { url = "http://192.168.1.1/" }), CancellationToken.None),
            Throws.TypeOf<ToolException>().With.Message.Contains("local network"));
        Assert.That(browser.Read, Is.Empty);
    }

    [Test]
    public void A_failure_of_a_page_is_one_sentence_for_the_model()
    {
        Assert.Multiple(() =>
        {
            Assert.That(async () => await Fetch().InvokeAsync(Arguments(new { }), CancellationToken.None),
                Throws.TypeOf<ToolException>().With.Message.Contains("'url'"));
            Assert.That(async () => await Fetch().InvokeAsync(Arguments(new { url = "https://example.com/absent" }), CancellationToken.None),
                Throws.TypeOf<ToolException>().With.Message.EqualTo("The site could not be reached."));
            Assert.That(async () => await Fetch(withBrowser: false).InvokeAsync(Arguments(new { url = "https://example.com/a" }), CancellationToken.None),
                Throws.TypeOf<ToolException>().With.Message.Contains("not available"));
        });
    }

    [Test]
    public async Task Without_a_key_the_search_goes_through_the_browser_with_the_chosen_engine()
    {
        await settings.WriteAsync(WebToolsOptions.Section, new WebToolsOptions { Engine = WebSearchEngine.Startpage, Results = 2 });

        using var result = JsonDocument.Parse(await Search().InvokeAsync(Arguments(new { query = "météo Montréal" }), CancellationToken.None));

        var root = result.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(browser.Searched, Is.EqualTo(new[] { (WebSearchEngine.Startpage, "météo Montréal", 2) }));
            Assert.That(api.Searched, Is.Empty);
            Assert.That(root.GetProperty("provider").GetString(), Is.EqualTo("startpage"));
            Assert.That(root.GetProperty("query").GetString(), Is.EqualTo("météo Montréal"));
            Assert.That(root.GetProperty("results").GetArrayLength(), Is.EqualTo(2));
            Assert.That(root.GetProperty("results")[0].GetProperty("url").GetString(), Is.EqualTo("https://example.com/1"));
            Assert.That(root.GetProperty("results")[0].GetProperty("title").GetString(), Is.EqualTo("Résultat 1"));
        });
    }

    [Test]
    public async Task With_a_key_in_the_vault_the_search_goes_through_Brave()
    {
        vault.Secrets[WebSearchApiSecrets.BraveKey.Value] = "key";

        using var result = JsonDocument.Parse(await Search().InvokeAsync(Arguments(new { query = "dotnet" }), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(api.Searched, Is.EqualTo(new[] { "dotnet" }));
            Assert.That(browser.Searched, Is.Empty);
            Assert.That(result.RootElement.GetProperty("provider").GetString(), Is.EqualTo("brave"));
        });
    }

    [Test]
    public async Task A_list_of_results_stays_within_the_size_of_a_result()
    {
        browser.SnippetLength = 2000;
        await settings.WriteAsync(WebToolsOptions.Section, new WebToolsOptions { Results = 10 });

        var text = await Search().InvokeAsync(Arguments(new { query = "long" }), CancellationToken.None);
        using var result = JsonDocument.Parse(text);

        Assert.Multiple(() =>
        {
            Assert.That(text, Has.Length.LessThanOrEqualTo(WebToolsOptions.MostResultCharacters));
            Assert.That(result.RootElement.GetProperty("results").GetArrayLength(), Is.InRange(1, 9));
        });
    }

    [Test]
    public void A_blocked_search_says_so_and_nothing_gets_around_it()
    {
        browser.FailWith = WebReadFailure.Blocked;

        Assert.That(async () => await Search().InvokeAsync(Arguments(new { query = "x" }), CancellationToken.None),
            Throws.TypeOf<ToolException>().With.Message.Contains("verify that a human"));
        Assert.That(browser.Searched, Has.Count.EqualTo(1), "One try, no retry on another engine.");
    }

    [Test]
    public void A_search_without_browser_nor_key_is_unavailable_and_a_missing_query_is_named()
    {
        Assert.Multiple(() =>
        {
            Assert.That(async () => await Search(withBrowser: false).InvokeAsync(Arguments(new { query = "x" }), CancellationToken.None),
                Throws.TypeOf<ToolException>().With.Message.Contains("not available"));
            Assert.That(async () => await Search().InvokeAsync(Arguments(new { }), CancellationToken.None),
                Throws.TypeOf<ToolException>().With.Message.Contains("'query'"));
        });
    }

    private sealed class FakeBrowser : IReadingBrowser
    {
        public Dictionary<string, WebPage> Pages { get; } = new(StringComparer.Ordinal);

        public List<(WebSearchEngine Engine, string Query, int Most)> Searched { get; } = [];

        public List<Uri> Read { get; } = [];

        public WebReadFailure? FailWith { get; set; }

        public int SnippetLength { get; set; } = 40;

        public ValueTask<IReadOnlyList<WebSearchResult>> SearchAsync(string query, WebSearchEngine engine, int most, CancellationToken cancellationToken = default)
        {
            Searched.Add((engine, query, most));
            if (FailWith is { } failure)
            {
                throw new WebReadException(failure);
            }

            return ValueTask.FromResult<IReadOnlyList<WebSearchResult>>(
                [.. Enumerable.Range(1, most).Select(index => new WebSearchResult($"Résultat {index}", new Uri($"https://example.com/{index}"), new string('x', SnippetLength)))]);
        }

        public ValueTask<WebPage> ReadAsync(Uri address, CancellationToken cancellationToken = default)
        {
            Read.Add(address);
            return Pages.TryGetValue(address.AbsoluteUri, out var page) ? ValueTask.FromResult(page) : throw new WebReadException(WebReadFailure.Unreachable);
        }

        public ValueTask ShowAsync(Uri? address = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask ClearDataAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class FakeSearchApi : IWebSearchApi
    {
        public List<string> Searched { get; } = [];

        public ValueTask<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int most, CancellationToken cancellationToken = default)
        {
            Searched.Add(query);
            return ValueTask.FromResult<IReadOnlyList<WebSearchResult>>([new WebSearchResult("Brave", new Uri("https://brave.example/1"), "extrait")]);
        }
    }
}
