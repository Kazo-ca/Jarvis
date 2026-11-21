using System.Text.Json;
using Kazo.Jarvis.Kernel.Web;
using Kazo.Jarvis.Windows.Browser;

namespace Kazo.Jarvis.Tests.Unit.Windows.Browser;

/// <summary>
/// The pages of the search engines (ADR 0022, section 4): the address of a search and the reading of what the
/// script of a page returned. The scripts themselves run in Chromium in the end-to-end tests.
/// </summary>
[TestFixture]
public sealed class SearchPagesTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    [TestCase(WebSearchEngine.DuckDuckGo, "https://html.duckduckgo.com/html/?q=m%C3%A9t%C3%A9o%20%26%20neige")]
    [TestCase(WebSearchEngine.Startpage, "https://www.startpage.com/sp/search?query=m%C3%A9t%C3%A9o%20%26%20neige")]
    [TestCase(WebSearchEngine.Bing, "https://www.bing.com/search?q=m%C3%A9t%C3%A9o%20%26%20neige")]
    public void The_query_is_escaped_in_the_address_of_the_search(WebSearchEngine engine, string expected) =>
        Assert.That(SearchPages.SearchAddress(engine, "météo & neige").AbsoluteUri, Is.EqualTo(expected));

    [Test]
    public void Every_engine_has_a_script()
    {
        foreach (var engine in Enum.GetValues<WebSearchEngine>())
        {
            Assert.That(SearchPages.SearchScript(engine), Does.StartWith("(() => {").And.Not.Contain("__CFG__"), engine.ToString());
        }

        Assert.That(SearchPages.PageScript, Does.Not.Contain("__MOST__"));
    }

    [Test]
    public void The_results_of_the_engine_selectors_win_over_the_generic_ones()
    {
        var reading = SearchPages.ReadSearch(Json("""
            {"specific":[{"title":"A","url":"https://a.example/","snippet":"extrait"},{"title":"B","url":"https://b.example/","snippet":""}],
             "generic":[{"title":"G","url":"https://g.example/","snippet":""}],"block":""}
            """), 5);

        Assert.Multiple(() =>
        {
            Assert.That(reading!.Results.Select(result => result.Title), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(reading.Results[0].Snippet, Is.EqualTo("extrait"));
            Assert.That(reading.Block, Is.Empty);
        });
    }

    [Test]
    public void The_generic_results_are_the_net_when_the_selectors_find_nothing()
    {
        var reading = SearchPages.ReadSearch(Json("""{"specific":[],"generic":[{"title":"G","url":"https://g.example/","snippet":"s"}],"block":""}"""), 5);

        Assert.That(reading!.Results.Single().Address.AbsoluteUri, Is.EqualTo("https://g.example/"));
    }

    [Test]
    public void Only_web_addresses_with_a_title_are_results_each_once_and_bounded()
    {
        var reading = SearchPages.ReadSearch(Json("""
            {"specific":[
              {"title":"Script","url":"javascript:alert(1)","snippet":""},
              {"title":"File","url":"file:///C:/x","snippet":""},
              {"title":"","url":"https://untitled.example/","snippet":""},
              {"title":"Relative","url":"/local","snippet":""},
              {"title":"One","url":"https://one.example/","snippet":""},
              {"title":"One again","url":"https://one.example/","snippet":""},
              {"title":"Two","url":"http://two.example/","snippet":""},
              {"title":"Three","url":"https://three.example/","snippet":""}],
             "generic":[],"block":"captcha-text"}
            """), 2);

        Assert.Multiple(() =>
        {
            Assert.That(reading!.Results.Select(result => result.Title), Is.EqualTo(new[] { "One", "Two" }));
            Assert.That(reading.Block, Is.EqualTo("captcha-text"));
        });
    }

    [Test]
    public void What_is_not_an_object_is_no_reading()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SearchPages.ReadSearch(default, 5), Is.Null);
            Assert.That(SearchPages.ReadSearch(Json("null"), 5), Is.Null);
            Assert.That(SearchPages.ReadPage(Json("\"text\"")), Is.Null);
            Assert.That(SearchPages.ReadSearch(Json("{}"), 5)!.Results, Is.Empty);
        });
    }

    [Test]
    public void A_page_is_read_with_its_final_address()
    {
        var page = SearchPages.ReadPage(Json("""{"url":"https://www.example.com/a","title":"Titre","text":"Le texte.","block":""}"""));

        Assert.Multiple(() =>
        {
            Assert.That(page!.Address, Is.EqualTo("https://www.example.com/a"));
            Assert.That(page.Title, Is.EqualTo("Titre"));
            Assert.That(page.Text, Is.EqualTo("Le texte."));
            Assert.That(page.Block, Is.Empty);
        });
    }
}
