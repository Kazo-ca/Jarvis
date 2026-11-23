using System.Text.Json;
using Kazo.Jarvis.Kernel.Web;

namespace Kazo.Jarvis.Windows.Browser;

/// <summary>What the script of a results page found.</summary>
/// <param name="Results">The results read, best first.</param>
/// <param name="Block">Why the page looks blocked (CAPTCHA, consent wall), or an empty text.</param>
internal sealed record SearchPageReading(IReadOnlyList<WebSearchResult> Results, string Block);

/// <summary>What the script of a page read.</summary>
/// <param name="Address">The address the page ended on.</param>
/// <param name="Title">Its title.</param>
/// <param name="Text">Its readable text.</param>
/// <param name="Block">Why the page looks blocked, or an empty text.</param>
internal sealed record PageReading(string Address, string Title, string Text, string Block);

/// <summary>
/// The pages of the search engines and how they are read (ADR 0022, section 4; spike E15): the address of a search,
/// the script that extracts title, address and snippet, and the reading of what the script returns. No WebView2
/// type here, so it is tested without Windows. The scripts only read the page: they click nothing and fill nothing.
/// </summary>
internal static class SearchPages
{
    /// <summary>The longest text taken from one page; a tool then returns it by parts.</summary>
    public const int MostPageCharacters = 200_000;

    /// <summary>The most results taken from one results page.</summary>
    public const int MostResults = 10;

    private sealed record Engine(string Address, string OwnHost, string Item, string Link, string? Title, string? Snippet);

    // Selectors proven by spike E15 on 2026-11-20. When an engine changes its markup, the generic reading of the
    // script (a link that holds a heading, outside the engine's own host) still finds the results.
    private static readonly Dictionary<WebSearchEngine, Engine> Engines = new()
    {
        [WebSearchEngine.DuckDuckGo] = new("https://html.duckduckgo.com/html/?q={q}", @"(^|\.)duckduckgo\.com$",
            ".result:not(.result--ad)", "a.result__a", null, ".result__snippet"),
        [WebSearchEngine.Startpage] = new("https://www.startpage.com/sp/search?query={q}", @"(^|\.)startpage\.com$",
            ".result, .w-gl__result", "a.result-title, a.w-gl__result-title, a.result-link", "h2, h3, .wgl-title",
            ".description, p.w-gl__description"),
        [WebSearchEngine.Bing] = new("https://www.bing.com/search?q={q}", @"(^|\.)bing\.com$",
            "#b_results > li.b_algo", "h2 a", null, ".b_caption p, p[class*=\"b_lineclamp\"], .b_algoSlug"),
    };

    /// <summary>The address of the results page of <paramref name="engine"/> for <paramref name="query"/>.</summary>
    public static Uri SearchAddress(WebSearchEngine engine, string query) =>
        new(Engines[engine].Address.Replace("{q}", Uri.EscapeDataString(query), StringComparison.Ordinal));

    /// <summary>The script that reads the results page of <paramref name="engine"/>; it returns an object read by <see cref="ReadSearch"/>.</summary>
    public static string SearchScript(WebSearchEngine engine)
    {
        var definition = Engines[engine];
        return ExtractScript.Replace("__CFG__", JsonSerializer.Serialize(new
        {
            own = definition.OwnHost,
            item = definition.Item,
            link = definition.Link,
            title = definition.Title,
            snippet = definition.Snippet,
            most = MostResults,
        }), StringComparison.Ordinal);
    }

    /// <summary>The script that reads the text of a page; it returns an object read by <see cref="ReadPage"/>.</summary>
    public static string PageScript { get; } = ReadScript.Replace("__MOST__", MostPageCharacters.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>Reads what <see cref="SearchScript"/> returned. The results of the engine's own selectors win; the generic ones are the net.</summary>
    /// <param name="value">The value of the script.</param>
    /// <param name="most">The most results kept.</param>
    /// <returns>The results and the block, or <see langword="null"/> when the script returned nothing usable.</returns>
    public static SearchPageReading? ReadSearch(JsonElement value, int most)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var specific = Items(value, "specific", most);
        var results = specific.Count > 0 ? specific : Items(value, "generic", most);
        return new SearchPageReading(results, Text(value, "block"));
    }

    /// <summary>Reads what <see cref="PageScript"/> returned.</summary>
    /// <param name="value">The value of the script.</param>
    /// <returns>The page, or <see langword="null"/> when the script returned nothing usable.</returns>
    public static PageReading? ReadPage(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object
            ? new PageReading(Text(value, "url"), Text(value, "title"), Text(value, "text"), Text(value, "block"))
            : null;

    private static List<WebSearchResult> Items(JsonElement value, string name, int most)
    {
        var results = new List<WebSearchResult>();
        if (!value.TryGetProperty(name, out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (results.Count >= most)
            {
                break;
            }

            // A page writes what it wants in a link: only http and https addresses are results.
            if (item.ValueKind == JsonValueKind.Object
                && Uri.TryCreate(Text(item, "url"), UriKind.Absolute, out var address)
                && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps)
                && Text(item, "title") is { Length: > 0 } title
                && !results.Any(result => result.Address == address))
            {
                results.Add(new WebSearchResult(title, address, Text(item, "snippet")));
            }
        }

        return results;
    }

    private static string Text(JsonElement value, string name) =>
        value.TryGetProperty(name, out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() ?? string.Empty : string.Empty;

    private const string BlockScript = """
  const blockOf = () => {
    const txt = clean(document.body ? document.body.innerText : '').slice(0, 4000).toLowerCase();
    const href = location.href.toLowerCase();
    if (/\/sorry\/|captcha|\/challenge/.test(href)) return 'captcha-url';
    if (document.querySelector('iframe[src*="recaptcha"],iframe[src*="hcaptcha"],iframe[src*="challenges.cloudflare.com"],.g-recaptcha,.h-captcha,#challenge-form')) return 'captcha-widget';
    if (/unusual traffic|trafic inhabituel|not a robot|pas un robot|bots use duckduckgo|select all squares|verify you are human|confirm you.re human|vérifiez que vous|prove you.re human|are you a robot/.test(txt)) return 'captcha-text';
    if (/^consent\./.test(location.hostname)) return 'consent-wall';
    return '';
  };
""";

    private const string ExtractScript = """
(() => {
  const cfg = __CFG__;
  const own = new RegExp(cfg.own);
  const clean = s => (s || '').replace(/\s+/g, ' ').trim();
  const text = el => el ? (el.innerText || el.textContent || '') : '';
  const decode = href => {
    try {
      const u = new URL(href, location.href);
      if (/duckduckgo\.com$/.test(u.hostname) && u.pathname.startsWith('/l/')) return u.searchParams.get('uddg') || u.href;
      if (/bing\.com$/.test(u.hostname) && u.pathname.startsWith('/ck/')) {
        let p = u.searchParams.get('u') || '';
        if (p.startsWith('a1')) { p = p.slice(2).replace(/-/g, '+').replace(/_/g, '/'); while (p.length % 4) p += '='; return atob(p); }
      }
      return u.href;
    } catch { return href; }
  };
  const external = url => { try { const u = new URL(url); return /^https?:$/.test(u.protocol) && !own.test(u.hostname); } catch { return false; } };
""" + BlockScript + """
  const specific = [];
  for (const it of document.querySelectorAll(cfg.item)) {
    const a = it.querySelector(cfg.link); if (!a) continue;
    const url = decode(a.getAttribute('href') || ''); if (!external(url)) continue;
    const t = cfg.title ? (it.querySelector(cfg.title) || a) : a;
    const title = clean(text(t)).slice(0, 200); if (!title) continue;
    const sn = cfg.snippet ? clean(text(it.querySelector(cfg.snippet))) : '';
    if (!specific.some(r => r.url === url)) specific.push({ title, url, snippet: sn.slice(0, 300) });
    if (specific.length >= cfg.most) break;
  }
  const generic = [];
  for (const h of document.querySelectorAll('h2, h3')) {
    const a = h.closest('a[href]') || h.querySelector('a[href]'); if (!a) continue;
    const url = decode(a.getAttribute('href') || ''); if (!external(url)) continue;
    const title = clean(text(h)).slice(0, 200); if (title.length < 3) continue;
    let c = a.parentElement, sn = '';
    for (let i = 0; i < 6 && c; i++, c = c.parentElement) { const t = clean(text(c)); if (t.length > title.length + 60) { sn = t.replace(title, '').trim(); break; } }
    if (!generic.some(r => r.url === url)) generic.push({ title, url, snippet: sn.slice(0, 300) });
    if (generic.length >= cfg.most) break;
  }
  return { specific, generic, block: blockOf() };
})()
""";

    // The longest of main, article and [role=main], else the body (spike E15: the first « article » of a page can be
    // a small block). Fixed and sticky layers (cookie banners) are left out of the text when they can be told apart.
    private const string ReadScript = """
(() => {
  const clean = s => (s || '').replace(/\s+/g, ' ').trim();
""" + BlockScript + """
  const textOf = el => ((el && (el.innerText || el.textContent)) || '').replace(/[ \t]+\n/g, '\n').replace(/\n{3,}/g, '\n\n').trim();
  let best = '';
  for (const el of document.querySelectorAll('main, article, [role="main"]')) { const t = textOf(el); if (t.length > best.length) best = t; }
  const body = textOf(document.body);
  if (best.length < 400 && body.length > best.length) best = body;
  return { url: location.href, title: clean(document.title).slice(0, 200), text: best.slice(0, __MOST__), block: blockOf() };
})()
""";
}
