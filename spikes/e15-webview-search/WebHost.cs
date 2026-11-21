using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;

namespace Spikes.WebViewSearch;

internal enum WindowMode { NeverShown, ShownNoActivate }

internal sealed record HostOptions(WindowMode Mode, bool ControllerVisible);

internal sealed record NavResult(bool Ok, int Status, string Error, double Ms);

internal sealed record MemSample(int Processes, double PrivateMb, double WorkingSetMb);

/// <summary>Everything the read-only guards saw (URIs are test or search URLs only).</summary>
internal sealed class GuardLog
{
    public List<string> Downloads { get; } = [];
    public List<string> NewWindows { get; } = [];
    public List<string> Permissions { get; } = [];
    public List<string> ExternalSchemes { get; } = [];
    public List<string> Dialogs { get; } = [];
    public List<string> BlockedNavigations { get; } = [];
    public List<string> BlockedRequests { get; } = [];
    public List<string> Redirects { get; } = [];
    public int WebMessages { get; set; }
    public int BasicAuth { get; set; }
}

/// <summary>
/// A WebView2 controller hosted in a WPF window that never takes activation:
/// WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW, no taskbar button, bottom-right corner.
/// </summary>
internal sealed class WebHost : IAsyncDisposable
{
    public const string VirtualHost = "e15.test";

    private readonly Dictionary<string, bool> dnsCache = new(StringComparer.OrdinalIgnoreCase);

    public required Window Window { get; init; }
    public required IntPtr Hwnd { get; init; }
    public required CoreWebView2Environment Env { get; init; }
    public required CoreWebView2Controller Controller { get; init; }
    public CoreWebView2 Core => Controller.CoreWebView2;
    public GuardLog Guards { get; } = new();
    public bool BlockPrivate { get; set; } = true;
    public double EnvMs { get; private init; }
    public double ControllerMs { get; private init; }
    public double DisposeMs { get; private set; }
    public bool BrowserExited { get; private set; }

    public static async Task<WebHost> CreateAsync(Paths paths, HostOptions options)
    {
        var sw = Stopwatch.StartNew();
        var window = CreateWindow(options.Mode, out var hwnd);
        var env = await CoreWebView2Environment.CreateAsync(null, paths.Profile, new CoreWebView2EnvironmentOptions());
        var envMs = sw.Elapsed.TotalMilliseconds;
        var controller = await env.CreateCoreWebView2ControllerAsync(hwnd);
        Native.GetClientRect(hwnd, out var rc);
        controller.Bounds = new System.Drawing.Rectangle(0, 0, Math.Max(1, rc.Right), Math.Max(1, rc.Bottom));
        controller.IsVisible = options.ControllerVisible;
        var host = new WebHost
        {
            Window = window, Hwnd = hwnd, Env = env, Controller = controller,
            EnvMs = Math.Round(envMs), ControllerMs = Math.Round(sw.Elapsed.TotalMilliseconds - envMs),
        };
        host.Configure(paths);
        return host;
    }

    private static Window CreateWindow(WindowMode mode, out IntPtr hwnd)
    {
        var area = SystemParameters.WorkArea;
        var window = new Window
        {
            Title = "E15 WebView2 spike",
            Width = 480, Height = 360,
            Left = area.Right - 490, Top = area.Bottom - 370,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false, ShowActivated = false, Topmost = false, Focusable = false,
        };
        hwnd = new WindowInteropHelper(window).EnsureHandle();
        var ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
        ex = (ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW) & ~Native.WS_EX_APPWINDOW;
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(ex));
        if (mode == WindowMode.ShownNoActivate) window.Show();
        return window;
    }

    private void Configure(Paths paths)
    {
        var s = Core.Settings;
        s.AreHostObjectsAllowed = false;
        s.IsWebMessageEnabled = false;
        s.AreDefaultScriptDialogsEnabled = false;
        s.AreDevToolsEnabled = false;
        s.AreDefaultContextMenusEnabled = false;
        s.IsStatusBarEnabled = false;
        s.IsZoomControlEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;
        s.IsPasswordAutosaveEnabled = false;
        s.IsGeneralAutofillEnabled = false;
        Core.IsMuted = true;
        Core.Profile.DefaultDownloadFolderPath = paths.Downloads;
        Core.SetVirtualHostNameToFolderMapping(VirtualHost, paths.Pages, CoreWebView2HostResourceAccessKind.DenyCors);

        Core.DownloadStarting += (_, e) =>
        {
            e.Cancel = true;
            e.Handled = true;
            Guards.Downloads.Add(Short(e.DownloadOperation.Uri));
        };
        Core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            Guards.NewWindows.Add(e.Uri);
            var target = e.Uri;
            Window.Dispatcher.BeginInvoke(() => Core.Navigate(target));
        };
        Core.PermissionRequested += (_, e) =>
        {
            e.State = CoreWebView2PermissionState.Deny;
            e.Handled = true;
            Guards.Permissions.Add(e.PermissionKind.ToString());
        };
        Core.WebMessageReceived += (_, _) => Guards.WebMessages++;
        Core.ScriptDialogOpening += (_, e) => Guards.Dialogs.Add(e.Kind.ToString());
        Core.LaunchingExternalUriScheme += (_, e) =>
        {
            e.Cancel = true;
            Guards.ExternalSchemes.Add(e.Uri);
        };
        Core.BasicAuthenticationRequested += (_, e) => { e.Cancel = true; Guards.BasicAuth++; };
        Core.NavigationStarting += (_, e) => CheckNavigation(e.Uri, e.IsRedirected, () => e.Cancel = true);
        Core.FrameNavigationStarting += (_, e) => CheckNavigation(e.Uri, e.IsRedirected, () => e.Cancel = true);
        Core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
        Core.WebResourceRequested += OnResourceRequested;
    }

    private void CheckNavigation(string uri, bool redirected, Action cancel)
    {
        if (redirected) Guards.Redirects.Add(Short(uri));
        if (!BlockPrivate || !Uri.TryCreate(uri, UriKind.Absolute, out var u)) return;
        if (!UrlGuard.IsPrivateLiteral(u)) return;
        cancel();
        Guards.BlockedNavigations.Add((redirected ? "redirect " : "") + uri);
    }

    /// <summary>A fake public http origin served by the host: /redirect?to=URL (302) and any other path = SyntheticPage.</summary>
    public const string PublicTestHost = "e15-public.example";

    public string SyntheticPage { get; set; } = "<!doctype html><body>synthetic</body>";

    private CoreWebView2WebResourceResponse Synthetic(Uri u)
    {
        if (u.AbsolutePath == "/redirect" && u.Query.StartsWith("?to=", StringComparison.Ordinal))
        {
            var to = Uri.UnescapeDataString(u.Query[4..]);
            return Env.CreateWebResourceResponse(null, 302, "Found", $"Location: {to}");
        }
        var body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(SyntheticPage));
        return Env.CreateWebResourceResponse(body, 200, "OK", "Content-Type: text/html");
    }

    private async void OnResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri = e.Request.Uri;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u) || !UrlGuard.IsWebScheme(u)) return;
        if (u.IdnHost.Equals(PublicTestHost, StringComparison.OrdinalIgnoreCase))
        {
            e.Response = Synthetic(u);
            return;
        }
        if (!BlockPrivate) return;
        if (u.IdnHost.Equals(VirtualHost, StringComparison.OrdinalIgnoreCase)) return;
        if (UrlGuard.IsPrivateLiteral(u))
        {
            Block(e, uri, "literal");
            return;
        }
        if (UrlGuard.IsIpLiteral(u)) return;
        var deferral = e.GetDeferral();
        try
        {
            if (await ResolvesPrivateAsync(u.IdnHost)) Block(e, uri, "dns");
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void Block(CoreWebView2WebResourceRequestedEventArgs e, string uri, string why)
    {
        e.Response = Env.CreateWebResourceResponse(null, 403, "Blocked", "Content-Type: text/plain");
        Guards.BlockedRequests.Add($"{why} {e.ResourceContext} {uri}");
    }

    private async Task<bool> ResolvesPrivateAsync(string host)
    {
        if (dnsCache.TryGetValue(host, out var cached)) return cached;
        bool result;
        try
        {
            using var cts = new CancellationTokenSource(2000);
            var addresses = await Dns.GetHostAddressesAsync(host, cts.Token);
            result = addresses.Any(UrlGuard.IsPrivate);
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or OperationCanceledException)
        {
            result = false; // unresolvable: Chromium cannot connect either
        }
        dnsCache[host] = result;
        return result;
    }

    public async Task<NavResult> NavigateAsync(string url, int timeoutMs = 20000)
    {
        var sw = Stopwatch.StartNew();
        var done = new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>();
        ulong? id = null;
        void Started(object? s, CoreWebView2NavigationStartingEventArgs e) => id ??= e.NavigationId;
        void Completed(object? s, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (id is not null && e.NavigationId == id) done.TrySetResult(e);
        }
        Core.NavigationStarting += Started;
        Core.NavigationCompleted += Completed;
        try
        {
            Core.Navigate(url);
            var finished = await Task.WhenAny(done.Task, Task.Delay(timeoutMs)) == done.Task;
            if (!finished) return new NavResult(false, 0, "timeout", Math.Round(sw.Elapsed.TotalMilliseconds));
            var r = done.Task.Result;
            return new NavResult(r.IsSuccess, r.HttpStatusCode, r.WebErrorStatus.ToString(), Math.Round(sw.Elapsed.TotalMilliseconds));
        }
        finally
        {
            Core.NavigationStarting -= Started;
            Core.NavigationCompleted -= Completed;
        }
    }

    public async Task<JsonElement> EvalAsync(string script)
    {
        var raw = await Core.ExecuteScriptAsync(script);
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.Clone();
    }

    public IReadOnlyList<int> ProcessIds => Env.GetProcessInfos().Select(p => p.ProcessId).ToList();

    public MemSample Memory()
    {
        long priv = 0, ws = 0;
        var n = 0;
        foreach (var info in Env.GetProcessInfos())
        {
            try
            {
                using var p = Process.GetProcessById(info.ProcessId);
                priv += p.PrivateMemorySize64;
                ws += p.WorkingSet64;
                n++;
            }
            catch (ArgumentException)
            {
                // process exited between listing and reading
            }
        }
        return new MemSample(n, Math.Round(priv / 1048576.0, 1), Math.Round(ws / 1048576.0, 1));
    }

    /// <summary>Captures the rendered view and reads one pixel (top quarter, centre).</summary>
    public async Task<object> CaptureAsync()
    {
        using var ms = new MemoryStream();
        var sw = Stopwatch.StartNew();
        try
        {
            await Core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, ms);
        }
        catch (Exception ex)
        {
            return new { bytes = 0L, pixel = "error " + ex.GetType().Name, ms = Math.Round(sw.Elapsed.TotalMilliseconds) };
        }
        var captureMs = Math.Round(sw.Elapsed.TotalMilliseconds);
        if (ms.Length == 0) return new { bytes = 0L, pixel = "empty", ms = captureMs };
        ms.Position = 0;
        var frame = new PngBitmapDecoder(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        var bmp = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var px = new byte[4];
        bmp.CopyPixels(new Int32Rect(bmp.PixelWidth / 2, bmp.PixelHeight / 4, 1, 1), px, 4, 0);
        return new { bytes = ms.Length, width = bmp.PixelWidth, height = bmp.PixelHeight, pixel = $"#{px[2]:X2}{px[1]:X2}{px[0]:X2}", ms = captureMs };
    }

    public async ValueTask DisposeAsync()
    {
        var exited = new TaskCompletionSource();
        Env.BrowserProcessExited += (_, _) => exited.TrySetResult();
        var sw = Stopwatch.StartNew();
        Controller.Close();
        Window.Close();
        BrowserExited = await Task.WhenAny(exited.Task, Task.Delay(15000)) == exited.Task;
        DisposeMs = Math.Round(sw.Elapsed.TotalMilliseconds);
    }

    private static string Short(string uri) => uri.Length > 120 ? uri[..120] : uri;
}
