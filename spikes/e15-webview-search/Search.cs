using System.Diagnostics;
using System.Text.Json;

namespace Spikes.WebViewSearch;

internal sealed record ResultItem(string Title, string Url, string Snippet);

internal sealed record SearchRecord(
    string Engine, int Query, string Lang, string Kind, double Ms, bool NavOk, int Status, string NavError,
    int Specific, int Generic, int Overlap, string Block, bool ConsentBanner, string FinalUrl, string PageTitle,
    List<ResultItem> Results);

internal static class Search
{
    private const int ExtractDeadlineMs = 10000;
    private const int PollMs = 300;

    public static async Task<SearchRecord> RunAsync(WebHost host, Engine engine, int index)
    {
        var q = Engine.Queries[index];
        var sw = Stopwatch.StartNew();
        var nav = await host.NavigateAsync(engine.Url(q.Text));
        JsonElement r = default;
        while (true)
        {
            try
            {
                r = await host.EvalAsync(engine.Script);
            }
            catch (Exception ex)
            {
                Log.Line($"eval failed {engine.Id}#{index}: {ex.GetType().Name}");
            }
            var ready = r.ValueKind == JsonValueKind.Object;
            var count = ready ? r.GetProperty("specific").GetArrayLength() : 0;
            var block = ready ? r.GetProperty("block").GetString() : "";
            if (count >= 3 || sw.ElapsedMilliseconds > ExtractDeadlineMs) break;
            if (!string.IsNullOrEmpty(block) && sw.ElapsedMilliseconds > 3000) break;
            await Task.Delay(PollMs);
        }
        var ms = Math.Round(sw.Elapsed.TotalMilliseconds);
        if (r.ValueKind != JsonValueKind.Object)
        {
            return new SearchRecord(engine.Id, index, q.Lang, q.Kind, ms, nav.Ok, nav.Status, nav.Error,
                0, 0, 0, "no-script-result", false, "", "", []);
        }
        var specific = Items(r.GetProperty("specific"));
        var generic = Items(r.GetProperty("generic"));
        var overlap = specific.Count(s => generic.Any(g => g.Url == s.Url));
        var blockText = specific.Count >= 3 ? "" : r.GetProperty("block").GetString() ?? "";
        return new SearchRecord(engine.Id, index, q.Lang, q.Kind, ms, nav.Ok, nav.Status, nav.Error,
            specific.Count, generic.Count, overlap, blockText, r.GetProperty("consentBanner").GetBoolean(),
            r.GetProperty("url").GetString() ?? "", r.GetProperty("title").GetString() ?? "",
            specific.Count > 0 ? specific : generic);
    }

    private static List<ResultItem> Items(JsonElement array) =>
        array.EnumerateArray()
            .Select(e => new ResultItem(e.GetProperty("title").GetString() ?? "", e.GetProperty("url").GetString() ?? "",
                e.GetProperty("snippet").GetString() ?? ""))
            .ToList();

    public static object Summarize(IEnumerable<SearchRecord> records)
    {
        return records.GroupBy(r => r.Engine).Select(g =>
        {
            var list = g.OrderBy(r => r.Query).ToList();
            var firstBlock = list.FirstOrDefault(r => r.Block != "");
            return new
            {
                engine = g.Key,
                queries = list.Count,
                successSpecific = list.Count(r => r.Specific >= 3),
                successGeneric = list.Count(r => r.Generic >= 3),
                avgOverlap = Math.Round(list.Average(r => (double)r.Overlap), 1),
                blocks = list.Count(r => r.Block != ""),
                blockKinds = list.Where(r => r.Block != "").Select(r => r.Block).Distinct().ToArray(),
                firstBlockAtQuery = firstBlock is null ? (int?)null : list.IndexOf(firstBlock) + 1,
                consentBanner = list.Count(r => r.ConsentBanner),
                medianMs = Report.Median(list.Where(r => r.Specific >= 3).Select(r => r.Ms)),
                worstMs = list.Count == 0 ? 0 : list.Max(r => r.Ms),
                example = list.FirstOrDefault(r => r.Results.Count > 0)?.Results.FirstOrDefault(),
            };
        }).ToList();
    }

    private const string ReadScript = """
(() => {
  const el = document.querySelector('main, article, [role="main"]');
  const src = el ? el.tagName.toLowerCase() : 'body';
  const text = ((el || document.body || {}).innerText || '').replace(/\n{3,}/g, '\n\n').trim();
  const vw = innerWidth * innerHeight;
  const overlays = [];
  for (const n of document.querySelectorAll('body *')) {
    const cs = getComputedStyle(n);
    if ((cs.position !== 'fixed' && cs.position !== 'sticky') || cs.display === 'none' || cs.visibility === 'hidden') continue;
    const r = n.getBoundingClientRect(); const area = r.width * r.height; if (area < 1) continue;
    const sig = ((n.id || '') + ' ' + (typeof n.className === 'string' ? n.className : '')).toLowerCase();
    const cookie = /cookie|consent|gdpr|onetrust|didomi|cmp|privacy|truste/.test(sig) || /cookie|témoin|consent/i.test((n.innerText || '').slice(0, 300));
    if (cookie || area > vw * 0.2) overlays.push({ tag: n.tagName.toLowerCase(), cookie, pct: Math.round(100 * area / vw) });
    if (overlays.length >= 8) break;
  }
  const cmpFrames = document.querySelectorAll('iframe[src*="consent"],iframe[id*="sp_message"],iframe[src*="cmp"]').length;
  return { src, len: text.length, words: text.split(/\s+/).length, overlays, cmpFrames };
})()
""";

    public static async Task<object> ReadPageAsync(WebHost host, string url)
    {
        var sw = Stopwatch.StartNew();
        var nav = await host.NavigateAsync(url, 25000);
        await Task.Delay(1500);
        JsonElement r = default;
        try
        {
            r = await host.EvalAsync(ReadScript);
        }
        catch (Exception ex)
        {
            Log.Line($"read eval failed: {ex.GetType().Name}");
        }
        var host0 = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;
        return new
        {
            host = host0,
            navOk = nav.Ok, status = nav.Status, navMs = nav.Ms,
            totalMs = Math.Round(sw.Elapsed.TotalMilliseconds),
            extract = r.ValueKind == JsonValueKind.Object ? (object)r : "no result",
        };
    }
}
