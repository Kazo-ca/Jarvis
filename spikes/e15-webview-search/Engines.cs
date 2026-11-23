using System.Text.Json;

namespace Spikes.WebViewSearch;

internal sealed record Query(string Lang, string Kind, string Text);

/// <summary>
/// A search engine: its URL, engine-specific selectors, and the host its own links live on.
/// The same script also runs a generic heuristic (links that hold an h2/h3) to compare stability.
/// </summary>
internal sealed record Engine(string Id, string UrlTemplate, string OwnHost, string Item, string Link, string? Title, string? Snippet)
{
    public string Url(string q) => UrlTemplate.Replace("{q}", Uri.EscapeDataString(q));

    public string Script => ExtractScript.Replace("__CFG__", JsonSerializer.Serialize(new
    {
        own = OwnHost, item = Item, link = Link, title = Title, snippet = Snippet,
    }));

    public static readonly Engine[] All =
    [
        new("ddg-html", "https://html.duckduckgo.com/html/?q={q}", @"(^|\.)duckduckgo\.com$",
            ".result:not(.result--ad)", "a.result__a", null, ".result__snippet"),
        new("ddg", "https://duckduckgo.com/?q={q}&ia=web", @"(^|\.)duckduckgo\.com$",
            "article[data-testid=\"result\"]", "a[data-testid=\"result-title-a\"]", null, "[data-result=\"snippet\"]"),
        new("bing", "https://www.bing.com/search?q={q}", @"(^|\.)bing\.com$",
            "#b_results > li.b_algo", "h2 a", null, ".b_caption p, p[class*=\"b_lineclamp\"], .b_algoSlug"),
        new("google", "https://www.google.com/search?q={q}", @"(^|\.)google\.[a-z.]+$",
            "#rso div.MjjYud, #rso div.g", "a:has(h3)", "h3", ".VwiC3b, [data-sncf], div[style*=\"line-clamp\"]"),
        new("brave", "https://search.brave.com/search?q={q}&source=web", @"(^|\.)brave\.com$",
            "#results .snippet[data-type=\"web\"]", "a[href^=\"http\"]", ".title, .search-snippet-title",
            ".snippet-description, .generic-snippet .content, .description"),
        new("startpage", "https://www.startpage.com/sp/search?query={q}", @"(^|\.)startpage\.com$",
            ".result, .w-gl__result", "a.result-title, a.w-gl__result-title, a.result-link", "h2, h3, .wgl-title",
            ".description, p.w-gl__description"),
    ];

    public static readonly Query[] Queries =
    [
        new("fr", "weather", "météo Montréal demain"),
        new("fr", "dotnet", "classe HttpClient .NET exemple"),
        new("fr", "news", "actualités Québec aujourd'hui"),
        new("fr", "recipe", "recette pâté chinois"),
        new("fr", "product", "aspirateur robot meilleur rapport qualité prix"),
        new("en", "weather", "weather Montreal tomorrow"),
        new("en", "dotnet", "System.Text.Json JsonSerializer.Deserialize"),
        new("en", "news", "latest news artificial intelligence regulation"),
        new("en", "recipe", "banana bread recipe"),
        new("en", "product", "Raspberry Pi 5 price"),
    ];

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
      if (/google\./.test(u.hostname) && u.pathname === '/url') return u.searchParams.get('q') || u.searchParams.get('url') || u.href;
      return u.href;
    } catch { return href; }
  };
  const external = url => { try { const u = new URL(url); return /^https?:$/.test(u.protocol) && !own.test(u.hostname); } catch { return false; } };
  const specific = [];
  for (const it of document.querySelectorAll(cfg.item)) {
    const a = it.querySelector(cfg.link); if (!a) continue;
    const url = decode(a.getAttribute('href') || ''); if (!external(url)) continue;
    const t = cfg.title ? (it.querySelector(cfg.title) || a) : a;
    const title = clean(text(t)); if (!title) continue;
    const sn = cfg.snippet ? clean(text(it.querySelector(cfg.snippet))) : '';
    if (!specific.some(r => r.url === url)) specific.push({ title, url, snippet: sn.slice(0, 300) });
    if (specific.length >= 5) break;
  }
  const generic = [];
  for (const h of document.querySelectorAll('h2, h3')) {
    const a = h.closest('a[href]') || h.querySelector('a[href]'); if (!a) continue;
    const url = decode(a.getAttribute('href') || ''); if (!external(url)) continue;
    const title = clean(text(h)); if (title.length < 3) continue;
    let c = a.parentElement, sn = '';
    for (let i = 0; i < 6 && c; i++, c = c.parentElement) { const t = clean(text(c)); if (t.length > title.length + 60) { sn = t.replace(title, '').trim(); break; } }
    if (!generic.some(r => r.url === url)) generic.push({ title, url, snippet: sn.slice(0, 300) });
    if (generic.length >= 5) break;
  }
  const txt = clean(document.body ? document.body.innerText : '').slice(0, 4000).toLowerCase();
  const href = location.href.toLowerCase();
  let block = '';
  if (/\/sorry\/|captcha|\/challenge/.test(href)) block = 'captcha-url';
  else if (document.querySelector('iframe[src*="recaptcha"],iframe[src*="hcaptcha"],iframe[src*="challenges.cloudflare.com"],.g-recaptcha,.h-captcha,#challenge-form,[data-sitekey]')) block = 'captcha-widget';
  else if (/unusual traffic|trafic inhabituel|not a robot|pas un robot|bots use duckduckgo|select all squares|verify you are human|confirm you.re human|vérifiez que vous|prove you.re human|are you a robot/.test(txt)) block = 'captcha-text';
  else if (/^consent\./.test(location.hostname) || /before you continue|avant d.accéder|avant de continuer/.test(txt)) block = 'consent-wall';
  const consentBanner = !!document.querySelector('[id*="consent" i],[class*="consent" i],[id*="cookie" i],[class*="cookie" i],#onetrust-banner-sdk,#didomi-host');
  return { url: location.origin + location.pathname, title: document.title.slice(0, 80), specific, generic, block, consentBanner, textLen: txt.length };
})()
""";
}
