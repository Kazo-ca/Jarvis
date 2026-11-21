using System.IO;
using System.Text.Json;

namespace Spikes.WebViewSearch;

/// <summary>Host-enforced read-only checks, each against a small local test page on https://e15.test/.</summary>
internal static class Security
{
    private const string LanAddress = "192.168.255.254"; // private range, nothing expected to answer

    private const string SubresPage = """
<!doctype html><html><body>subres<script>
const P = '{PORT}'; const tag = new URLSearchParams(location.search).get('tag') || 'x';
const add = (t, src) => { const e = document.createElement(t); e.src = src; document.body.appendChild(e); };
const rd = path => 'http://e15-public.example/redirect?to=' + encodeURIComponent('http://127.0.0.1:' + P + '/' + tag + path);
const w = window.__sub = {};
const f = (k, u) => fetch(u, { mode: 'no-cors' }).then(r => w[k] = 'ok ' + r.type, e => w[k] = e.name);
add('img', 'http://127.0.0.1:' + P + '/' + tag + '-img');
add('iframe', 'http://127.0.0.1:' + P + '/' + tag + '-frame');
add('script', 'http://127.0.0.1:' + P + '/' + tag + '-script');
f('fetch', 'http://127.0.0.1:' + P + '/' + tag + '-fetch');
f('dnsFetch', 'http://localtest.me:' + P + '/' + tag + '-dnsfetch');
add('img', rd('-redirimg'));
f('redirFetch', rd('-redirfetch'));
if (tag.startsWith('on')) { add('img', 'http://{LAN}/on-lanimg'); f('lanFetch', 'http://{LAN}/on-lanfetch'); }
</script></body></html>
""";

    private const string PermScript = """
(() => { const p = window.__perm = {};
  navigator.mediaDevices.getUserMedia({ video: true }).then(() => p.camera = 'granted', e => p.camera = e.name);
  navigator.mediaDevices.getUserMedia({ audio: true }).then(() => p.mic = 'granted', e => p.mic = e.name);
  navigator.geolocation.getCurrentPosition(() => p.geo = 'granted', e => p.geo = 'error code ' + e.code, { timeout: 4000 });
  Notification.requestPermission().then(r => p.notifications = r, e => p.notifications = e.name);
  return 'started'; })()
""";

    public static async Task<int> RunAsync(Paths paths)
    {
        using var server = new LoopbackServer();
        using var focus = new FocusWatch { LocalDetailsFile = Path.Combine(paths.Out, "focus-security.local.json") };
        var host = await focus.Guard("create", () => WebHost.CreateAsync(paths, new HostOptions(WindowMode.NeverShown, true)));
        WritePages(paths, server.Port, host);
        focus.SetOurs(host.ProcessIds);
        var r = new Dictionary<string, object?>();
        string? violation = null;
        try
        {
            var s = host.Core.Settings;
            r["settings"] = new
            {
                s.IsPasswordAutosaveEnabled, s.IsGeneralAutofillEnabled, s.AreHostObjectsAllowed,
                s.IsWebMessageEnabled, s.AreDefaultScriptDialogsEnabled, s.AreDevToolsEnabled,
            };
            r["hostObjects"] = await HostObjectsAsync(host, focus);
            r["dialogs"] = await DialogsAsync(host, focus);
            r["permissions"] = await PermissionsAsync(host, focus);
            r["newWindow"] = await NewWindowAsync(host, focus);
            r["download"] = await DownloadAsync(host, focus, paths);
            r["externalScheme"] = await ExternalSchemeAsync(host, focus);
            host.BlockPrivate = false;
            r["network-control-unguarded"] = await NetworkAsync(host, focus, server, "ctl");
            host.BlockPrivate = true;
            r["network-guarded"] = await NetworkAsync(host, focus, server, "on");
        }
        catch (FocusViolationException ex)
        {
            violation = ex.Message;
        }
        await host.DisposeAsync();
        r["allServerHits"] = server.Hits.ToArray();
        r["focus"] = focus.Summary();
        r["violation"] = violation;
        Report.Write(paths, "security", r);
        return violation is null ? 0 : 2;
    }

    private static void WritePages(Paths paths, int port, WebHost host)
    {
        void W(string name, string html) => File.WriteAllText(Path.Combine(paths.Pages, name), html);
        W("hostobj.html", "<!doctype html><body>hostobj</body>");
        W("perm.html", "<!doctype html><body>perm</body>");
        W("target.html", "<!doctype html><body>target</body>");
        W("newwin.html", "<!doctype html><body><a id=\"l\" target=\"_blank\" href=\"target.html?via=blank\">open</a></body>");
        W("download.html", "<!doctype html><style>a{display:block;height:60px;font-size:30px}</style><body><a id=\"d\" href=\"data:application/octet-stream;base64,SGVsbG8=\" download=\"e15.bin\">dl</a>"
            + "<a id=\"m\" href=\"mailto:nobody@example.invalid\">mail</a><a id=\"x\" href=\"e15-nonexistent:link\">x</a></body>");
        W("subres.html", SubresPage.Replace("{PORT}", port.ToString()).Replace("{LAN}", LanAddress));
        host.SyntheticPage = SubresPage.Replace("{PORT}", port.ToString()).Replace("{LAN}", LanAddress);
        File.WriteAllBytes(Path.Combine(paths.Pages, "file.bin"), [1, 2, 3, 4]);
    }

    private static Task<NavResult> Page(WebHost h, FocusWatch f, string page) =>
        f.Guard("nav " + page, () => h.NavigateAsync($"https://{WebHost.VirtualHost}/{page}"));

    private static async Task<object> HostObjectsAsync(WebHost h, FocusWatch f)
    {
        await Page(h, f, "hostobj.html");
        var page = await h.EvalAsync("""
({ webview: typeof (window.chrome && window.chrome.webview),
   hostObjects: typeof (window.chrome && window.chrome.webview && window.chrome.webview.hostObjects),
   post: (() => { try { window.chrome.webview.postMessage('x'); return 'sent'; } catch (e) { return 'error ' + e.name; } })() })
""");
        await Task.Delay(500);
        return new { page, webMessagesReceived = h.Guards.WebMessages };
    }

    private static async Task<object> DialogsAsync(WebHost h, FocusWatch f)
    {
        await Page(h, f, "target.html");
        await h.EvalAsync("setTimeout(() => { window.__d = [String(alert('x')), String(confirm('y')), String(prompt('z'))]; }, 0); 'ok'");
        await Task.Delay(1000);
        return new { page = await h.EvalAsync("window.__d"), dialogEvents = h.Guards.Dialogs.ToArray() };
    }

    private static async Task<object> PermissionsAsync(WebHost h, FocusWatch f)
    {
        await Page(h, f, "perm.html");
        await h.EvalAsync(PermScript);
        JsonElement page = default;
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(300);
            page = await h.EvalAsync("window.__perm");
            if (page.ValueKind == JsonValueKind.Object && page.EnumerateObject().Count() >= 4) break;
        }
        return new { page, requestedKinds = h.Guards.Permissions.ToArray() };
    }

    private static async Task<object> NewWindowAsync(WebHost h, FocusWatch f)
    {
        await Page(h, f, "newwin.html");
        await h.EvalAsync("window.open('https://e15.test/target.html?via=open'); 'ok'");
        await Task.Delay(1500);
        var afterOpen = h.Core.Source;
        await Page(h, f, "newwin.html");
        await h.EvalAsync("document.getElementById('l').click(); 'ok'");
        await Task.Delay(1500);
        return new { afterWindowOpen = afterOpen, afterTargetBlank = h.Core.Source, events = h.Guards.NewWindows.ToArray() };
    }

    private static async Task<object> DownloadAsync(WebHost h, FocusWatch f, Paths paths)
    {
        await Page(h, f, "download.html");
        await h.EvalAsync("document.getElementById('d').click(); 'ok'");
        await Task.Delay(1500);
        var nav = await Page(h, f, "file.bin");
        await Task.Delay(1500);
        return new
        {
            fileBinNav = nav,
            events = h.Guards.Downloads.ToArray(),
            filesInDownloadFolder = Directory.GetFiles(paths.Downloads).Length,
        };
    }

    private static async Task<object> ExternalSchemeAsync(WebHost h, FocusWatch f)
    {
        await Page(h, f, "download.html");
        await f.Guard("external via API", async () => { h.Core.Navigate("e15-nonexistent:api"); await Task.Delay(1500); });
        await f.Guard("external via link", async () => { await h.EvalAsync("document.getElementById('x').click(); 'ok'"); await Task.Delay(1500); });
        var unregistered = h.Guards.ExternalSchemes.Count;
        await f.Guard("external mailto link", async () => { await h.EvalAsync("document.getElementById('m').click(); 'ok'"); await Task.Delay(1500); });
        await f.Guard("external mailto API", async () => { h.Core.Navigate("mailto:nobody@example.invalid"); await Task.Delay(1500); });
        var withoutGesture = h.Guards.ExternalSchemes.Count;
        // Trusted click (user gesture) through the WebView2 DevTools-protocol API, no debugging port.
        await Page(h, f, "download.html");
        await f.Guard("external trusted click x", () => TrustedClickAsync(h, "x"));
        await Page(h, f, "download.html");
        await f.Guard("external trusted click mailto", () => TrustedClickAsync(h, "m"));
        return new
        {
            eventsForUnregisteredScheme = unregistered,
            eventsWithoutGesture = withoutGesture,
            events = h.Guards.ExternalSchemes.ToArray(),
            sourceAfter = h.Core.Source,
        };
    }

    private static async Task TrustedClickAsync(WebHost h, string id)
    {
        var rect = await h.EvalAsync($"(() => {{ const r = document.getElementById('{id}').getBoundingClientRect(); return {{ x: r.x + r.width / 2, y: r.y + r.height / 2 }}; }})()");
        var x = rect.GetProperty("x").GetDouble();
        var y = rect.GetProperty("y").GetDouble();
        foreach (var type in new[] { "mousePressed", "mouseReleased" })
        {
            var args = JsonSerializer.Serialize(new { type, x, y, button = "left", clickCount = 1 });
            await h.Core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", args);
        }
        await Task.Delay(1500);
    }

    private static async Task<object> NetworkAsync(WebHost h, FocusWatch f, LoopbackServer server, string tag)
    {
        var p = server.Port;
        var blockedNavBefore = h.Guards.BlockedNavigations.Count;
        var blockedReqBefore = h.Guards.BlockedRequests.Count;
        var external = "https://httpbingo.org/redirect-to?url=" + Uri.EscapeDataString($"http://127.0.0.1:{p}/{tag}-redirtop");
        var synthetic = $"http://{WebHost.PublicTestHost}/redirect?to=" + Uri.EscapeDataString($"http://127.0.0.1:{p}/{tag}-synredirtop");
        var tops = new List<string> { $"http://127.0.0.1:{p}/{tag}-top", $"http://localtest.me:{p}/{tag}-dnstop", external, synthetic };
        if (tag == "on")
        {
            tops.AddRange([
                $"http://{LanAddress}/on-lantop", $"http://2130706433:{p}/on-decimal", $"http://0x7f.0.0.1:{p}/on-hex",
                $"http://[::1]:{p}/on-v6", $"http://localhost:{p}/on-localhost", $"http://127.0.0.1.nip.io:{p}/on-nip",
            ]);
        }
        var navs = new List<object>();
        foreach (var url in tops)
        {
            var nav = await f.Guard("top " + url, () => h.NavigateAsync(url, 15000));
            navs.Add(new { url, nav.Ok, nav.Status, nav.Error, landedOn = h.Core.Source });
        }
        await Page(h, f, $"subres.html?tag={tag}");
        await Task.Delay(5000);
        var subHttps = await h.EvalAsync("window.__sub");
        await f.Guard("nav synthetic http page", () => h.NavigateAsync($"http://{WebHost.PublicTestHost}/subres.html?tag={tag}p"));
        await Task.Delay(5000);
        var subHttp = await h.EvalAsync("window.__sub");
        return new
        {
            topNavigations = navs,
            subresourceFromHttpsPage = subHttps,
            subresourceFromHttpPage = subHttp,
            serverHits = server.HitsWithPrefix(tag + "-"),
            serverHitsFromHttpPage = server.HitsWithPrefix(tag + "p-"),
            blockedNavigations = h.Guards.BlockedNavigations.Skip(blockedNavBefore).ToArray(),
            blockedRequests = h.Guards.BlockedRequests.Skip(blockedReqBefore).ToArray(),
        };
    }
}
