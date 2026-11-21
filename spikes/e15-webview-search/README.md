# E15 — Web search through an in-app WebView2

Proves that Kazo.Jarvis can search the web with a browser **inside the application**: a WebView2
controller hosted in a WPF window that never takes the keyboard focus, driven only through the WebView2
API (`Microsoft.Web.WebView2` 1.0.3179.45, Evergreen runtime). The installed Edge or Chrome is never
started; there is no Playwright, no Selenium and no remote-debugging port.

Results: [RESULTS.md](RESULTS.md) (French).

## What it does

- `WebHost.cs` creates a borderless WPF window (480×360, bottom-right corner, `ShowInTaskbar=false`,
  `ShowActivated=false`, `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`), keeps it hidden unless asked otherwise,
  and attaches a `CoreWebView2Controller` to its HWND. It applies the read-only guards: downloads cancelled,
  new windows redirected into the same view, permissions denied, web messages and host objects off,
  script dialogs suppressed, external URI schemes cancelled, password autosave and autofill off, and
  loopback/private addresses blocked in `NavigationStarting`, `FrameNavigationStarting` and
  `WebResourceRequested` (IP literals, `localhost`, and host names that resolve to private addresses).
- `Engines.cs` lists the engines, the 10 fixed queries (5 French, 5 English) and the extraction script
  (engine-specific selectors plus a generic "link holding an h2/h3" heuristic, run side by side).
- `Focus.cs` samples `GetForegroundWindow` every 100 ms and checks it before/after every creation,
  navigation and show; a change to a spike process stops the variant.
- `Security.cs` runs the read-only tests against local pages (`https://e15.test/`, a virtual host mapped
  to a folder), a synthetic plain-http origin served by the host, and a loopback HTTP server that counts
  the requests it receives.

## Run

Needs Windows, the .NET 10 SDK and the WebView2 Evergreen runtime.

```powershell
dotnet build spikes/e15-webview-search -c Release -o C:\jarvis\temp\e15-webview-search\bin
$exe = 'C:\jarvis\temp\e15-webview-search\bin\WebViewSearch.exe'
Start-Process $exe -ArgumentList probe -Wait                      # hidden vs shown, throttling, focus
Start-Process $exe -ArgumentList security -Wait                   # read-only guards
Start-Process $exe -ArgumentList search,--label,main -Wait        # 6 engines x 10 queries + 3 page reads
Start-Process $exe -ArgumentList profile-set -Wait                # cookie persistence ...
Start-Process $exe -ArgumentList profile-clear -Wait              # ... ClearBrowsingDataAsync
Start-Process $exe -ArgumentList profile-check -Wait
```

Options for `search`: `--shown` (window shown without activation), `--invisible`
(`CoreWebView2Controller.IsVisible=false`), `--engines ddg-html,bing`, `--root <folder>`.

Everything is written under `C:\jarvis\temp\e15-webview-search` (or `--root`): `profile\` (WebView2 user
data folder), `out\` (JSON measurements and logs), `pages\` (generated test pages), `downloads\` (must
stay empty). Nothing from there is committed except the summary in `measurements.json`.

The spike sends at most about 20 queries per engine per full run, at least 3 s apart for one engine. It
fills no form other than the search URL, signs in nowhere, and does not try to solve a CAPTCHA; it only
records that one appeared.
