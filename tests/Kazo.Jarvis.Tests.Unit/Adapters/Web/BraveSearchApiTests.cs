using System.Net;
using System.Text.Json;
using Kazo.Jarvis.Adapters.Web;
using Kazo.Jarvis.Kernel.Web;
using Kazo.Jarvis.Tests.Unit.Adapters.Llm;
using Kazo.Jarvis.Tests.Unit.Adapters.Secrets;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Web;

/// <summary>
/// Brave Search (ADR 0017, section 3), against answers written by hand: what is sent, how the results are read, how
/// a failure is named. Never called against the real service.
/// </summary>
[TestFixture]
public sealed class BraveSearchApiTests
{
    private const string Answer = """
        {"type":"search","web":{"results":[
          {"title":"<strong>Météo</strong> Montréal","url":"https://meteo.example/montreal","description":"Ciel d&#x27;hiver, <strong>-2</strong> degrés."},
          {"title":"Sans adresse","description":"ignored"},
          {"title":"Autre schéma","url":"ftp://files.example/a","description":"ignored"},
          {"url":"https://second.example/page"}
        ]}}
        """;

    private static FakeVault VaultWithKey()
    {
        var vault = new FakeVault();
        vault.Secrets[WebSearchApiSecrets.BraveKey.Value] = "the-key";
        return vault;
    }

    [Test]
    public async Task The_query_and_the_key_are_sent_and_the_results_are_read()
    {
        var handler = new StubHandler((_, _) => StubHandler.Json(Answer));
        var api = new BraveSearchApi(new HttpClient(handler), VaultWithKey());

        var results = await api.SearchAsync("météo à Montréal", 5);

        var request = handler.Received.Single().Request;
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.RequestUri!.GetLeftPart(UriPartial.Path), Is.EqualTo("https://api.search.brave.com/res/v1/web/search"));
            Assert.That(request.RequestUri.Query, Does.Contain("q=m%C3%A9t%C3%A9o%20%C3%A0%20Montr%C3%A9al").And.Contain("count=5"));
            Assert.That(request.Headers.GetValues("X-Subscription-Token"), Is.EqualTo(new[] { "the-key" }));
            Assert.That(results.Select(result => result.Address.AbsoluteUri), Is.EqualTo(new[] { "https://meteo.example/montreal", "https://second.example/page" }));
            Assert.That(results[0].Title, Is.EqualTo("Météo Montréal"), "The marks around the words found are removed.");
            Assert.That(results[0].Snippet, Is.EqualTo("Ciel d'hiver, -2 degrés."));
            Assert.That(results[1].Title, Is.EqualTo("second.example"), "Without a title, the host.");
        });
    }

    [Test]
    public void The_results_are_bounded()
    {
        using var document = JsonDocument.Parse(Answer);

        Assert.That(BraveSearchApi.Read(document.RootElement, 1), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task An_answer_without_results_is_an_empty_list()
    {
        var api = new BraveSearchApi(new HttpClient(new StubHandler((_, _) => StubHandler.Json("""{"type":"search"}"""))), VaultWithKey());

        Assert.That(await api.SearchAsync("x", 5), Is.Empty);
    }

    [Test]
    public void Without_a_key_nothing_is_sent()
    {
        var handler = new StubHandler((_, _) => StubHandler.Json(Answer));
        var api = new BraveSearchApi(new HttpClient(handler), new FakeVault());

        Assert.That(async () => await api.SearchAsync("x", 5),
            Throws.TypeOf<WebReadException>().With.Property(nameof(WebReadException.Failure)).EqualTo(WebReadFailure.KeyRejected));
        Assert.That(handler.Received, Is.Empty);
    }

    [TestCase(HttpStatusCode.Unauthorized, WebReadFailure.KeyRejected)]
    [TestCase(HttpStatusCode.Forbidden, WebReadFailure.KeyRejected)]
    [TestCase(HttpStatusCode.TooManyRequests, WebReadFailure.Blocked)]
    [TestCase(HttpStatusCode.InternalServerError, WebReadFailure.Unreachable)]
    public void A_refusal_is_named(HttpStatusCode status, WebReadFailure failure)
    {
        var api = new BraveSearchApi(new HttpClient(new StubHandler((_, _) => StubHandler.Json("{}", status))), VaultWithKey());

        Assert.That(async () => await api.SearchAsync("x", 5),
            Throws.TypeOf<WebReadException>().With.Property(nameof(WebReadException.Failure)).EqualTo(failure));
    }

    [Test]
    public void An_unreadable_answer_and_a_dead_line_are_named()
    {
        var garbled = new BraveSearchApi(new HttpClient(new StubHandler((_, _) => StubHandler.Json("<html>"))), VaultWithKey());
        var dead = new BraveSearchApi(new HttpClient(new StubHandler((_, _) => throw new HttpRequestException("no route"))), VaultWithKey());

        Assert.Multiple(() =>
        {
            Assert.That(async () => await garbled.SearchAsync("x", 5),
                Throws.TypeOf<WebReadException>().With.Property(nameof(WebReadException.Failure)).EqualTo(WebReadFailure.NothingRead));
            Assert.That(async () => await dead.SearchAsync("x", 5),
                Throws.TypeOf<WebReadException>().With.Property(nameof(WebReadException.Failure)).EqualTo(WebReadFailure.Unreachable));
        });
    }
}
