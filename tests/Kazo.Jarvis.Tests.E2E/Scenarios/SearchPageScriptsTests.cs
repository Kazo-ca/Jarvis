using System.Text.Json;
using Kazo.Jarvis.Kernel.Web;
using Kazo.Jarvis.Windows.Browser;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Kazo.Jarvis.Tests.E2E.Scenarios;

/// <summary>
/// The scripts the browser of Jarvis runs in a page (ADR 0022, sections 3 and 4), run here by Chromium against
/// pages written by hand and served in place of the engines: nothing leaves the test machine. They prove that the
/// scripts are valid and read what they should; the real pages of the engines are checked on Windows.
/// </summary>
[TestFixture]
public sealed class SearchPageScriptsTests : PageTest
{
    private const string DuckDuckGo = """
        <!doctype html><html><head><title>météo at DuckDuckGo</title></head><body>
        <div class="result result--ad"><h2><a class="result__a" href="https://ad.example/buy">Une annonce</a></h2></div>
        <div class="result results_links web-result">
          <h2 class="result__title"><a rel="nofollow" class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fmeteo.example%2Fmontreal%3Fa%3D1&amp;rut=abc">Météo   Montréal</a></h2>
          <a class="result__snippet" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fmeteo.example%2Fmontreal">Ciel dégagé ce soir, <b>neige</b> demain.</a>
        </div>
        <div class="result results_links web-result">
          <h2 class="result__title"><a class="result__a" href="https://duckduckgo.com/about">Une page du moteur</a></h2>
        </div>
        <div class="result results_links web-result">
          <h2 class="result__title"><a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fsecond.example%2F">Deuxième</a></h2>
          <a class="result__snippet">Un autre extrait.</a>
        </div>
        </body></html>
        """;

    private const string Bing = """
        <!doctype html><html><head><title>x - Bing</title></head><body>
        <ol id="b_results">
          <li class="b_algo"><h2><a href="https://www.bing.com/ck/a?!&amp;&amp;p=x&amp;u=a1aHR0cHM6Ly9leGFtcGxlLm9yZy9wYWdl&amp;ntb=1">Titre Bing</a></h2>
            <div class="b_caption"><p>Extrait de Bing.</p></div></li>
          <li class="b_ad"><h2><a href="https://ad.example/">Annonce</a></h2></li>
        </ol></body></html>
        """;

    private const string UnknownMarkup = """
        <!doctype html><html><head><title>x - Startpage</title></head><body>
        <section class="changed"><a href="https://one.example/a"><h3>Premier titre</h3></a>
          <p>Un extrait assez long pour être pris comme le texte qui accompagne ce résultat dans la page.</p></section>
        <section class="changed"><h2><a href="https://two.example/b">Second titre</a></h2></section>
        <section class="changed"><h2><a href="/settings">Réglages du moteur</a></h2></section>
        </body></html>
        """;

    private const string Captcha = """
        <!doctype html><html><head><title>DuckDuckGo</title></head><body>
        <p>Unfortunately, bots use DuckDuckGo too. Please complete the following challenge.</p>
        <p>Select all squares containing a duck.</p></body></html>
        """;

    private async Task<JsonElement> RunAsync(Uri address, string html, string script)
    {
        await Page.RouteAsync("**/*", route => route.FulfillAsync(new RouteFulfillOptions { ContentType = "text/html; charset=utf-8", Body = html }));
        await Page.GotoAsync(address.AbsoluteUri);
        return await Page.EvaluateAsync<JsonElement>(script);
    }

    [Test]
    public async Task The_results_of_DuckDuckGo_are_read_with_their_real_addresses()
    {
        var value = await RunAsync(SearchPages.SearchAddress(WebSearchEngine.DuckDuckGo, "météo"), DuckDuckGo, SearchPages.SearchScript(WebSearchEngine.DuckDuckGo));

        var reading = SearchPages.ReadSearch(value, 5);
        Assert.Multiple(() =>
        {
            Assert.That(reading!.Results.Select(result => result.Address.AbsoluteUri),
                Is.EqualTo(new[] { "https://meteo.example/montreal?a=1", "https://second.example/" }),
                "The redirection link of the engine is decoded; the ad and the engine's own page are left out.");
            Assert.That(reading.Results[0].Title, Is.EqualTo("Météo Montréal"));
            Assert.That(reading.Results[0].Snippet, Is.EqualTo("Ciel dégagé ce soir, neige demain."));
            Assert.That(reading.Block, Is.Empty);
        });
    }

    [Test]
    public async Task The_results_of_Bing_are_read_with_their_real_addresses()
    {
        var value = await RunAsync(SearchPages.SearchAddress(WebSearchEngine.Bing, "x"), Bing, SearchPages.SearchScript(WebSearchEngine.Bing));

        var result = SearchPages.ReadSearch(value, 5)!.Results.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Address.AbsoluteUri, Is.EqualTo("https://example.org/page"));
            Assert.That(result.Title, Is.EqualTo("Titre Bing"));
            Assert.That(result.Snippet, Is.EqualTo("Extrait de Bing."));
        });
    }

    [Test]
    public async Task When_an_engine_changes_its_markup_the_generic_reading_still_finds_the_results()
    {
        var value = await RunAsync(SearchPages.SearchAddress(WebSearchEngine.Startpage, "x"), UnknownMarkup, SearchPages.SearchScript(WebSearchEngine.Startpage));

        var reading = SearchPages.ReadSearch(value, 5);
        Assert.Multiple(() =>
        {
            Assert.That(reading!.Results.Select(result => result.Address.AbsoluteUri), Is.EqualTo(new[] { "https://one.example/a", "https://two.example/b" }));
            Assert.That(reading.Results[0].Title, Is.EqualTo("Premier titre"));
            Assert.That(reading.Results[0].Snippet, Does.StartWith("Un extrait assez long"));
        });
    }

    [Test]
    public async Task A_verification_page_is_seen_as_a_block()
    {
        var value = await RunAsync(SearchPages.SearchAddress(WebSearchEngine.DuckDuckGo, "x"), Captcha, SearchPages.SearchScript(WebSearchEngine.DuckDuckGo));

        var reading = SearchPages.ReadSearch(value, 5);
        Assert.Multiple(() =>
        {
            Assert.That(reading!.Results, Is.Empty);
            Assert.That(reading.Block, Is.EqualTo("captcha-text"));
        });
    }

    [Test]
    public async Task The_longest_main_part_of_a_page_is_its_text()
    {
        var body = string.Concat(Enumerable.Range(1, 40).Select(index => $"<p>Paragraphe {index} du contenu principal.</p>"));
        var html = $"<!doctype html><html><head><title>  Un   titre </title></head><body><nav>Menu Accueil Contact</nav><article>Petit bloc.</article><main>{body}</main><footer>Pied de page</footer></body></html>";

        var page = SearchPages.ReadPage(await RunAsync(new Uri("https://site.example/article?id=1"), html, SearchPages.PageScript));

        Assert.Multiple(() =>
        {
            Assert.That(page!.Address, Is.EqualTo("https://site.example/article?id=1"));
            Assert.That(page.Title, Is.EqualTo("Un titre"));
            Assert.That(page.Text, Does.StartWith("Paragraphe 1 du contenu principal.").And.Contain("Paragraphe 40"));
            Assert.That(page.Text, Does.Not.Contain("Menu").And.Not.Contain("Pied de page").And.Not.Contain("Petit bloc"));
            Assert.That(page.Block, Is.Empty);
        });
    }

    [Test]
    public async Task A_page_without_main_part_is_read_from_its_body_and_bounded()
    {
        var html = "<!doctype html><html><head><title>Simple</title></head><body><div>" + new string('a', SearchPages.MostPageCharacters + 500) + "</div></body></html>";

        var page = SearchPages.ReadPage(await RunAsync(new Uri("https://site.example/"), html, SearchPages.PageScript));

        Assert.That(page!.Text, Has.Length.EqualTo(SearchPages.MostPageCharacters));
    }
}
