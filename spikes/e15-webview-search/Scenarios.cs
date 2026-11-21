using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Spikes.WebViewSearch;

internal static class Scenarios
{
    private const int GapMs = 1000;
    private const int MaxConsecutiveBlocks = 3;

    private const string ThrottlePage = """
<!doctype html><html><head><style>html,body{margin:0;height:100%;background:#c00000;color:#fff;font:20px sans-serif}</style></head>
<body><div style="position:absolute;bottom:10px;left:10px">E15 throttle probe</div><script>
const m = window.__m = { raf: 0, timeouts: 0, intervals: 0, start: performance.now() };
function r() { m.raf++; requestAnimationFrame(r); } requestAnimationFrame(r);
function t() { m.timeouts++; setTimeout(t, 0); } t();
setInterval(() => m.intervals++, 100);
</script></body></html>
""";

    private const string ThrottleRead = """
(() => { const m = window.__m; const s = (performance.now() - m.start) / 1000;
  return { vis: document.visibilityState, hidden: document.hidden, seconds: Math.round(s * 10) / 10,
    rafPerS: Math.round(m.raf / s), timeoutsPerS: Math.round(m.timeouts / s), intervalsPerS: Math.round(m.intervals / s * 10) / 10 }; })()
""";

    // ---------- probe: hidden vs shown-without-activation ----------

    public static async Task<int> ProbeAsync(Paths paths)
    {
        File.WriteAllText(Path.Combine(paths.Pages, "throttle.html"), ThrottlePage);
        using var focus = new FocusWatch();
        var variants = new (string Name, WindowMode Mode, bool Visible, int Query)[]
        {
            ("never-shown/controller-visible", WindowMode.NeverShown, true, 0),
            ("never-shown/controller-invisible", WindowMode.NeverShown, false, 6),
            ("shown-noactivate/controller-visible", WindowMode.ShownNoActivate, true, 9),
        };
        var results = new List<Dictionary<string, object?>>();
        foreach (var v in variants)
        {
            var rec = new Dictionary<string, object?> { ["variant"] = v.Name };
            WebHost? host = null;
            try
            {
                var sw = Stopwatch.StartNew();
                host = await focus.Guard($"{v.Name} create", () => WebHost.CreateAsync(paths, new HostOptions(v.Mode, v.Visible)));
                rec["createMs"] = Math.Round(sw.Elapsed.TotalMilliseconds);
                rec["envMs"] = host.EnvMs;
                rec["controllerMs"] = host.ControllerMs;
                focus.SetOurs(host.ProcessIds);
                rec["windowVisible"] = Native.IsWindowVisible(host.Hwnd);
                Log.Line(v.Name + " created"); rec["throttle"] = await ThrottleAsync(host, focus, v.Name); Log.Line(v.Name + " throttle done");
                rec["capture"] = await host.CaptureAsync(); Log.Line(v.Name + " capture done");
                foreach (var id in new[] { "ddg-html", "bing" })
                {
                    var engine = Engine.All.First(e => e.Id == id);
                    var r = await focus.Guard($"{v.Name} search {id}", () => Search.RunAsync(host, engine, v.Query));
                    rec["search-" + id] = new { r.Ms, r.Specific, r.Block, r.NavOk }; Log.Line(v.Name + " search " + id + " done");
                    focus.SetOurs(host.ProcessIds);
                    await Task.Delay(3000);
                }
                if (v.Mode == WindowMode.NeverShown && v.Visible)
                {
                    await focus.Guard("show on demand", () => { host.Window.Show(); return Task.CompletedTask; });
                    await Task.Delay(500);
                    rec["afterShow"] = new
                    {
                        windowVisible = Native.IsWindowVisible(host.Hwnd),
                        throttle = await ThrottleAsync(host, focus, "after show"),
                        capture = await host.CaptureAsync(),
                    };
                }
            }
            catch (FocusViolationException ex)
            {
                rec["focusViolation"] = ex.Message;
            }
            finally
            {
                if (host is not null)
                {
                    await host.DisposeAsync();
                    rec["disposeMs"] = host.DisposeMs;
                    rec["browserExited"] = host.BrowserExited;
                }
            }
            results.Add(rec);
            Log.Line("probe " + JsonSerializer.Serialize(rec));
        }
        Report.Write(paths, "probe", new
        {
            results,
            focus = focus.Summary(),
            focusChecks = focus.Checks.Select(c => new { c.Op, c.Changed, c.AfterIsOurs }),
        });
        return 0;
    }

    private static async Task<object> ThrottleAsync(WebHost host, FocusWatch focus, string label)
    {
        var nav = await focus.Guard($"{label} navigate local", () => host.NavigateAsync($"https://{WebHost.VirtualHost}/throttle.html"));
        await Task.Delay(2000);
        return new { navOk = nav.Ok, navMs = nav.Ms, page = await host.EvalAsync(ThrottleRead) };
    }

    // ---------- search: engines, cost, page reading ----------

    public static async Task<int> SearchAsync(Paths paths, HostOptions options, string label, string[] engineIds, int maxQueries = 10)
    {
        using var focus = new FocusWatch { LocalDetailsFile = Path.Combine(paths.Out, $"focus-search-{label}.local.json") };
        var total = Stopwatch.StartNew();
        var host = await focus.Guard("create", () => WebHost.CreateAsync(paths, options));
        var createMs = Math.Round(total.Elapsed.TotalMilliseconds);
        focus.SetOurs(host.ProcessIds);
        await host.NavigateAsync("about:blank");
        await Task.Delay(3000);
        var memAfterCreate = host.Memory();

        var engines = Engine.All.Where(e => engineIds.Length == 0 || engineIds.Contains(e.Id)).ToList();
        var records = new List<SearchRecord>();
        var during = new List<MemSample>();
        var consecutive = engines.ToDictionary(e => e.Id, _ => 0);
        var stopped = new List<string>();
        double coldFirstResultMs = 0;
        string? violation = null;
        try
        {
            for (var qi = 0; qi < Math.Min(maxQueries, Engine.Queries.Length); qi++)
            {
                foreach (var engine in engines.Where(e => !stopped.Contains(e.Id)))
                {
                    var r = await focus.Guard($"search {engine.Id} #{qi}", () => Search.RunAsync(host, engine, qi));
                    if (records.Count == 0) coldFirstResultMs = Math.Round(total.Elapsed.TotalMilliseconds);
                    records.Add(r);
                    during.Add(host.Memory());
                    focus.SetOurs(host.ProcessIds);
                    Log.Line($"{engine.Id} #{qi} {r.Ms} ms specific={r.Specific} generic={r.Generic} block={r.Block} url={r.FinalUrl}");
                    consecutive[engine.Id] = r.Block != "" ? consecutive[engine.Id] + 1 : 0;
                    if (consecutive[engine.Id] >= MaxConsecutiveBlocks) stopped.Add(engine.Id);
                    await Task.Delay(engines.Count == 1 ? 3 * GapMs : GapMs);
                }
            }
        }
        catch (FocusViolationException ex)
        {
            violation = ex.Message;
        }

        await host.NavigateAsync("about:blank");
        await Task.Delay(5000);
        var memIdle = host.Memory();
        var reads = await ReadPagesAsync(host, focus, records);
        await host.DisposeAsync();

        Report.Write(paths, $"search-{label}-records", records);
        Report.Write(paths, $"search-{label}", new
        {
            options = new { window = options.Mode.ToString(), controllerVisible = options.ControllerVisible },
            cost = new
            {
                createMs, host.EnvMs, host.ControllerMs, coldFirstResultMs,
                firstSearchMs = records.ElementAtOrDefault(0)?.Ms,
                secondSearchMs = records.ElementAtOrDefault(1)?.Ms,
                memAfterCreate,
                memDuringMaxPrivateMb = during.Count == 0 ? 0 : during.Max(m => m.PrivateMb),
                memDuringMaxWorkingSetMb = during.Count == 0 ? 0 : during.Max(m => m.WorkingSetMb),
                memDuringMaxProcesses = during.Count == 0 ? 0 : during.Max(m => m.Processes),
                memIdleAfterSearch = memIdle,
                host.DisposeMs, host.BrowserExited,
            },
            engines = Search.Summarize(records),
            stoppedEngines = stopped,
            reads,
            focus = focus.Summary(),
            violation,
            guards = new
            {
                downloads = host.Guards.Downloads.Count, newWindows = host.Guards.NewWindows.Count,
                permissions = host.Guards.Permissions, externalSchemes = host.Guards.ExternalSchemes.Count,
                dialogs = host.Guards.Dialogs.Count, blockedRequests = host.Guards.BlockedRequests,
            },
        });
        return violation is null ? 0 : 2;
    }

    private static async Task<List<object>> ReadPagesAsync(WebHost host, FocusWatch focus, List<SearchRecord> records)
    {
        var picks = new[] { ("fr", "recipe"), ("en", "dotnet"), ("en", "news") };
        var reads = new List<object>();
        foreach (var (lang, kind) in picks)
        {
            var url = records.Where(r => r.Lang == lang && r.Kind == kind && r.Specific >= 3)
                .Select(r => r.Results[0].Url).FirstOrDefault();
            if (url is null) continue;
            try
            {
                reads.Add(await focus.Guard($"read {kind}", () => Search.ReadPageAsync(host, url)));
            }
            catch (FocusViolationException ex)
            {
                reads.Add(new { url, violation = ex.Message });
                break;
            }
            await Task.Delay(2000);
        }
        return reads;
    }

    // ---------- diag: page structure of an engine whose selectors failed (local output only) ----------

    private const string DiagScript = """
(() => {
  const q = s => document.querySelectorAll(s).length;
  const h3links = [...document.querySelectorAll('h3')].slice(0, 6).map(h => {
    const a = h.closest('a') || h.querySelector('a');
    return { text: (h.innerText || '').slice(0, 60), href: a ? (a.getAttribute('href') || '').slice(0, 100) : null,
      parents: [h.parentElement, h.parentElement && h.parentElement.parentElement].filter(Boolean).map(p => p.tagName + '.' + (typeof p.className === 'string' ? p.className : '')).join(' < ') };
  });
  const bing = [...document.querySelectorAll('#b_results > li.b_algo')].slice(0, 10).map(li => { const a = li.querySelector('h2 a'); if (!a) return 'no h2 a'; try { const u = new URL(a.href); return u.hostname + ' u=' + (u.searchParams.get('u') || '').slice(0, 6) + ' cite=' + ((li.querySelector('cite') || {}).innerText || '').slice(0, 40); } catch { return 'bad'; } });
  const gres = [...document.querySelectorAll('#rso div.MjjYud')].slice(0, 8).map(d => { const a = d.querySelector('a:has(h3)'); return a ? { attrs: [...a.attributes].map(x => x.name + '=' + x.value.slice(0, 50)), cite: ((d.querySelector('cite') || {}).innerText || '').slice(0, 60), httpLinks: [...d.querySelectorAll('a[href^="http"]')].slice(0, 2).map(x => x.href.slice(0, 60)) } : null; }).filter(Boolean);
  return { title: document.title, h2: q('h2'), h3: q('h3'), aH3: q('a h3'), rso: q('#rso'), search: q('#search'),
    mjj: q('div.MjjYud'), g: q('div.g'), vwi: q('.VwiC3b'), bAlgo: q('li.b_algo'), bingLis: bing, gres,
    text: (document.body ? document.body.innerText : '').slice(0, 400) };
})()
""";

    public static async Task<int> DiagAsync(Paths paths, string[] engineIds)
    {
        var host = await WebHost.CreateAsync(paths, new HostOptions(WindowMode.NeverShown, true));
        var output = new Dictionary<string, object>();
        foreach (var id in engineIds)
        {
            var engine = Engine.All.First(e => e.Id == id);
            await host.NavigateAsync(engine.Url(Engine.Queries[id == "bing" ? 5 : 9].Text));
            await Task.Delay(4000);
            output[id] = await host.EvalAsync(DiagScript); output[id + "-extract"] = await host.EvalAsync(engine.Script);
            await Task.Delay(3000);
        }
        await host.DisposeAsync();
        Report.Write(paths, "diag.local", output);
        return 0;
    }

    // ---------- profile: cookie persistence and clearing ----------

    public static async Task<int> ProfileAsync(Paths paths, string step)
    {
        var host = await WebHost.CreateAsync(paths, new HostOptions(WindowMode.NeverShown, false));
        var cm = host.Core.CookieManager;
        async Task<object> Count()
        {
            var all = await cm.GetCookiesAsync("");
            return new
            {
                cookies = all.Count,
                domains = all.Select(c => c.Domain).Distinct().Count(),
                probePresent = all.Any(c => c.Name == "e15probe"),
            };
        }
        var before = await Count();
        double clearMs = 0;
        if (step == "set")
        {
            var c = cm.CreateCookie("e15probe", "1", WebHost.VirtualHost, "/");
            c.Expires = DateTime.Now.AddDays(1);
            cm.AddOrUpdateCookie(c);
        }
        else if (step == "clear")
        {
            var sw = Stopwatch.StartNew();
            await host.Core.Profile.ClearBrowsingDataAsync();
            clearMs = Math.Round(sw.Elapsed.TotalMilliseconds);
        }
        var after = await Count();
        await host.DisposeAsync();
        Report.Write(paths, $"profile-{step}", new { step, before, after, clearMs, host.DisposeMs });
        return 0;
    }
}
