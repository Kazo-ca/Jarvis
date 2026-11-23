using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Windows.Threading;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace Kazo.Jarvis.Windows.Browser;

/// <summary>
/// The browser of Jarvis on Windows (ADR 0022): a WebView2 controller in a window of the host, with a data folder of
/// its own, apart from the interface of Jarvis and from the browser of the user. Jarvis never starts Edge or Chrome.
/// It is created at the first search, stays hidden while Jarvis reads, and stops after a time without use. The host
/// enforces the read-only rules whatever a page asks: no download, no new window, no camera, microphone, position or
/// notification, no other application, no saved password, and no address of this machine or of the local network,
/// at every navigation, redirection and resource. Built from spike E15.
/// </summary>
internal sealed partial class WebViewReadingBrowser : IReadingBrowser, IDisposable
{
    /// <summary>How long the browser stays open without use (spike E15: its memory does not come back by itself).</summary>
    internal static readonly TimeSpan IdleBeforeClosing = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan SearchNavigation = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PageNavigation = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan ExtractDeadline = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BlockPatience = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(300);

    /// <summary>After the load, the time left to the scripts of a page to write its text.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(1200);

    private readonly Dispatcher dispatcher;
    private readonly JarvisPaths paths;
    private readonly IWebAddressPolicy policy;
    private readonly ILogger<WebViewReadingBrowser> logger;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, bool> resolvedHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer idle;
    private ReadingBrowserWindow? window;
    private CoreWebView2Environment? environment;
    private CoreWebView2Controller? controller;
    private bool navigationRefused;
    private bool disposed;

    /// <summary>Creates the service. The browser itself is created at the first use.</summary>
    public WebViewReadingBrowser(Dispatcher dispatcher, JarvisPaths paths, IWebAddressPolicy policy, ILogger<WebViewReadingBrowser> logger)
    {
        this.dispatcher = dispatcher;
        this.paths = paths;
        this.policy = policy;
        this.logger = logger;
        idle = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = IdleBeforeClosing };
        idle.Tick += (_, _) => CloseWhenIdle();
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<WebSearchResult>> SearchAsync(string query, WebSearchEngine engine, int most, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return RunAsync(() => SearchOnUiAsync(query, engine, Math.Clamp(most, 1, SearchPages.MostResults), cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<WebPage> ReadAsync(Uri address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        return RunAsync(() => ReadOnUiAsync(address, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask ShowAsync(Uri? address = null, CancellationToken cancellationToken = default) =>
        await RunAsync(async () =>
        {
            var core = await EnsureAsync();
            window!.OpenForUser();
            PlaceView();
            if (address is not null && policy.Allows(address))
            {
                core.Navigate(address.AbsoluteUri);
            }

            return true;
        }, cancellationToken);

    /// <inheritdoc />
    public async ValueTask ClearDataAsync(CancellationToken cancellationToken = default) =>
        await RunAsync(async () =>
        {
            var core = await EnsureAsync();
            await core.Profile.ClearBrowsingDataAsync();
            core.Navigate("about:blank");
            LogCleared();
            return true;
        }, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            CloseBrowser();
        }
        else
        {
            dispatcher.Invoke(CloseBrowser);
        }

        gate.Dispose();
    }

    /// <summary>One use at a time, on the thread of the windows: WebView2 belongs to it.</summary>
    private async ValueTask<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await dispatcher.InvokeAsync(work).Task.Unwrap().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<WebSearchResult>> SearchOnUiAsync(string query, WebSearchEngine engine, int most, CancellationToken cancellationToken)
    {
        var core = await EnsureAsync();
        var clock = Stopwatch.StartNew();
        var navigation = await NavigateAsync(core, SearchPages.SearchAddress(engine, query), SearchNavigation, cancellationToken);
        if (navigation.TimedOut)
        {
            throw new WebReadException(WebReadFailure.TimedOut);
        }

        // The page may still be writing its results: read until enough are there, or until it clearly will not.
        var script = SearchPages.SearchScript(engine);
        var enough = Math.Min(3, most);
        SearchPageReading? reading = null;
        var extraction = Stopwatch.StartNew();
        while (true)
        {
            reading = SearchPages.ReadSearch(await EvaluateAsync(core, script), most) ?? reading;
            if (reading is { Results.Count: var count } && count >= enough)
            {
                break;
            }

            if (extraction.Elapsed > ExtractDeadline || (reading is { Block.Length: > 0 } && extraction.Elapsed > BlockPatience))
            {
                break;
            }

            await Task.Delay(Poll, cancellationToken);
        }

        // An engine may list anything: an address Jarvis may not read is not a result.
        var results = (reading?.Results ?? []).Where(result => policy.Allows(result.Address)).ToList();
        LogSearched(engine, results.Count, reading?.Block ?? string.Empty, navigation.Status, (long)clock.Elapsed.TotalMilliseconds);
        if (results.Count > 0)
        {
            return results;
        }

        // Jarvis does not get around a block (ADR 0022, section 4): the cause goes back to the model.
        if (reading is { Block.Length: > 0 } || navigation.Status is 403 or 429)
        {
            throw new WebReadException(WebReadFailure.Blocked);
        }

        throw new WebReadException(navigation.Succeeded ? WebReadFailure.NothingRead : WebReadFailure.Unreachable);
    }

    private async Task<WebPage> ReadOnUiAsync(Uri address, CancellationToken cancellationToken)
    {
        if (!policy.Allows(address))
        {
            throw new WebReadException(WebReadFailure.AddressRefused);
        }

        var core = await EnsureAsync();
        var clock = Stopwatch.StartNew();
        var navigation = await NavigateAsync(core, address, PageNavigation, cancellationToken);
        if (navigation.Refused)
        {
            throw new WebReadException(WebReadFailure.AddressRefused);
        }

        if (navigation.TimedOut)
        {
            throw new WebReadException(WebReadFailure.TimedOut);
        }

        if (!navigation.Succeeded && navigation.Status == 0)
        {
            throw new WebReadException(WebReadFailure.Unreachable);
        }

        await Task.Delay(Settle, cancellationToken);
        var reading = SearchPages.ReadPage(await EvaluateAsync(core, SearchPages.PageScript));
        LogRead(address.Host, reading?.Text.Length ?? 0, reading?.Block ?? string.Empty, navigation.Status, (long)clock.Elapsed.TotalMilliseconds);
        if (reading is null)
        {
            throw new WebReadException(WebReadFailure.NothingRead);
        }

        // A verification page has a little text of its own: it is not the page that was asked.
        if (reading.Block.Length > 0 && (reading.Text.Length < 600 || navigation.Status is 403 or 429))
        {
            throw new WebReadException(WebReadFailure.Blocked);
        }

        if (reading.Text.Length == 0)
        {
            throw new WebReadException(navigation.Status >= 400 ? WebReadFailure.Unreachable : WebReadFailure.NothingRead);
        }

        if (!Uri.TryCreate(reading.Address, UriKind.Absolute, out var final) || !policy.Allows(final))
        {
            throw new WebReadException(WebReadFailure.AddressRefused);
        }

        return new WebPage(final, reading.Title, reading.Text);
    }

    /// <summary>Creates the window, the environment and the controller at the first use, and restarts the idle clock.</summary>
    private async Task<CoreWebView2> EnsureAsync()
    {
        idle.Stop();
        idle.Start();
        if (controller is not null)
        {
            return controller.CoreWebView2;
        }

        try
        {
            window = new ReadingBrowserWindow();

            // Its own data folder: neither the profile of Edge nor the WebView2 that shows the interface (ADR 0022, section 2).
            environment = await CoreWebView2Environment.CreateAsync(null, paths.Browser, new CoreWebView2EnvironmentOptions());
            controller = await environment.CreateCoreWebView2ControllerAsync(window.Handle);

            // Visible in a window that is not shown: an invisible controller throttles the page (spike E15).
            controller.IsVisible = true;
            PlaceView();
            Configure(controller.CoreWebView2);
            window.ViewChanged += (_, _) => PlaceView();
            window.NavigateRequested += (_, typed) => NavigateForUser(typed);
            window.BackRequested += (_, _) =>
            {
                if (controller?.CoreWebView2 is { CanGoBack: true } core)
                {
                    core.GoBack();
                }
            };
            window.ReloadRequested += (_, _) => controller?.CoreWebView2.Reload();
            LogStarted();
            return controller.CoreWebView2;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // No WebView2 runtime, a locked data folder: the tools say the browser is not available.
            LogStartFailed(exception);
            CloseBrowser();
            throw new WebReadException(WebReadFailure.Unavailable, exception);
        }
    }

    private void PlaceView()
    {
        if (controller is not null && window is not null)
        {
            controller.Bounds = window.ViewBounds;
        }
    }

    /// <summary>The read-only rules of ADR 0022, section 3, enforced whatever a page asks.</summary>
    private void Configure(CoreWebView2 core)
    {
        var settings = core.Settings;

        // No bridge to the application (ADR 0022, section 2): a page can call nothing in Jarvis.
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = false;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        core.IsMuted = true;

        core.DownloadStarting += (_, e) =>
        {
            e.Cancel = true;
            e.Handled = true;
        };
        core.NewWindowRequested += (_, e) =>
        {
            // No new window: the link opens in the same view, through the same checks.
            e.Handled = true;
            var target = e.Uri;
            dispatcher.BeginInvoke(() =>
            {
                if (Uri.TryCreate(target, UriKind.Absolute, out var address) && policy.Allows(address))
                {
                    controller?.CoreWebView2.Navigate(address.AbsoluteUri);
                }
            });
        };
        core.PermissionRequested += (_, e) =>
        {
            e.State = CoreWebView2PermissionState.Deny;
            e.Handled = true;
        };
        core.LaunchingExternalUriScheme += (_, e) => e.Cancel = true;
        core.BasicAuthenticationRequested += (_, e) => e.Cancel = true;
        core.NavigationStarting += (_, e) =>
        {
            if (!MayNavigate(e.Uri))
            {
                e.Cancel = true;
                navigationRefused = true;
                LogRefused("navigation");
            }
        };
        core.FrameNavigationStarting += (_, e) =>
        {
            if (!MayNavigate(e.Uri))
            {
                e.Cancel = true;
                LogRefused("frame");
            }
        };
        core.SourceChanged += (_, _) => window?.ShowAddress(core.Source);
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
        core.WebResourceRequested += OnResourceRequested;
    }

    /// <summary>A navigation goes to an address the policy allows, or to the empty page. Nothing else: no file, no internal page.</summary>
    private bool MayNavigate(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var address))
        {
            return false;
        }

        // « about:blank », and « about:srcdoc » for a frame written by its page.
        return address.Scheme == "about" || policy.Allows(address);
    }

    /// <summary>
    /// Every request of a page, redirections and resources included: refused when its address is local as written,
    /// or once its name resolves to a local address (ADR 0022, section 3; spike E15).
    /// </summary>
    private async void OnResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var current = environment;
        if (current is null || !Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var address))
        {
            return;
        }

        // Web sockets go to a host too; the policy reads them as their http twin. Other schemes carry no host
        // (data, blob, about) or are refused by the navigation checks.
        var asWeb = address.Scheme switch
        {
            "http" or "https" => address,
            "ws" => new UriBuilder(address) { Scheme = "http", Port = address.IsDefaultPort ? -1 : address.Port }.Uri,
            "wss" => new UriBuilder(address) { Scheme = "https", Port = address.IsDefaultPort ? -1 : address.Port }.Uri,
            _ => null,
        };
        if (asWeb is null)
        {
            if (address.Scheme == "ftp")
            {
                Refuse(e, current, "scheme");
            }

            return;
        }

        if (!policy.Allows(asWeb))
        {
            Refuse(e, current, "address");
            return;
        }

        var host = asWeb.IdnHost;
        if (IPAddress.TryParse(host.Trim('[', ']'), out _))
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            if (!await ResolvesToAllowedAsync(host))
            {
                Refuse(e, current, "resolved");
            }
        }
        catch (Exception exception)
        {
            // Never leave a request open on a doubt: refuse it.
            LogResolveFailed(exception);
            Refuse(e, current, "error");
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void Refuse(CoreWebView2WebResourceRequestedEventArgs e, CoreWebView2Environment current, string why)
    {
        e.Response = current.CreateWebResourceResponse(null, 403, "Blocked", "Content-Type: text/plain");
        LogRefused(why);
    }

    /// <summary>Whether every address <paramref name="host"/> resolves to may be read. A name that does not resolve reaches nothing.</summary>
    private async Task<bool> ResolvesToAllowedAsync(string host)
    {
        if (resolvedHosts.TryGetValue(host, out var known))
        {
            return known;
        }

        bool allowed;
        try
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var addresses = await Dns.GetHostAddressesAsync(host, limit.Token);
            allowed = addresses.All(resolved => policy.AllowsResolved(host, resolved));
        }
        catch (Exception exception) when (exception is System.Net.Sockets.SocketException or OperationCanceledException)
        {
            allowed = true;
        }

        resolvedHosts[host] = allowed;
        return allowed;
    }

    /// <summary>What the user typed in the bar: an address, through the same checks as Jarvis.</summary>
    private void NavigateForUser(string typed)
    {
        var text = typed.Contains("://", StringComparison.Ordinal) ? typed : "https://" + typed;
        if (controller?.CoreWebView2 is { } core && Uri.TryCreate(text, UriKind.Absolute, out var address) && policy.Allows(address))
        {
            core.Navigate(address.AbsoluteUri);
        }
    }

    private async Task<Navigation> NavigateAsync(CoreWebView2 core, Uri address, TimeSpan limit, CancellationToken cancellationToken)
    {
        var done = new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>();
        ulong? id = null;
        void Started(object? sender, CoreWebView2NavigationStartingEventArgs e) => id ??= e.NavigationId;
        void Completed(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (id is not null && e.NavigationId == id)
            {
                done.TrySetResult(e);
            }
        }

        navigationRefused = false;
        core.NavigationStarting += Started;
        core.NavigationCompleted += Completed;
        try
        {
            core.Navigate(address.AbsoluteUri);
            var finished = await Task.WhenAny(done.Task, Task.Delay(limit, cancellationToken)) == done.Task;
            cancellationToken.ThrowIfCancellationRequested();
            if (!finished)
            {
                core.Stop();
                return new Navigation(false, 0, navigationRefused, TimedOut: true);
            }

            var result = await done.Task;
            return new Navigation(result.IsSuccess, result.HttpStatusCode, navigationRefused, TimedOut: false);
        }
        catch (OperationCanceledException)
        {
            // An interruption: stop loading, and leave nothing of the turn on screen.
            core.Stop();
            throw;
        }
        finally
        {
            core.NavigationStarting -= Started;
            core.NavigationCompleted -= Completed;
        }
    }

    private static async Task<JsonElement> EvaluateAsync(CoreWebView2 core, string script)
    {
        try
        {
            using var document = JsonDocument.Parse(await core.ExecuteScriptAsync(script));
            return document.RootElement.Clone();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // The page changed under the script: the caller reads again or gives up.
            return default;
        }
    }

    private void CloseWhenIdle()
    {
        // In use, or open in front of the user: look again later.
        if (window is { IsOpenForUser: true } || gate.CurrentCount == 0)
        {
            return;
        }

        idle.Stop();
        CloseBrowser();
        LogStopped();
    }

    private void CloseBrowser()
    {
        idle.Stop();
        try
        {
            controller?.Close();
            window?.CloseForGood();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            LogStartFailed(exception);
        }

        controller = null;
        environment = null;
        window = null;
        resolvedHosts.Clear();
    }

    private readonly record struct Navigation(bool Succeeded, int Status, bool Refused, bool TimedOut);

    [LoggerMessage(Level = LogLevel.Information, Message = "The browser of Jarvis started.")]
    private partial void LogStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "The browser of Jarvis stopped after a time without use.")]
    private partial void LogStopped();

    [LoggerMessage(Level = LogLevel.Information, Message = "The data of the browser of Jarvis was erased.")]
    private partial void LogCleared();

    [LoggerMessage(Level = LogLevel.Warning, Message = "The browser of Jarvis could not start or stop.")]
    private partial void LogStartFailed(Exception exception);

    // The words of a search and the path of a page stay out of the log: the engine, the host and the counts are enough.
    [LoggerMessage(Level = LogLevel.Information, Message = "Web search on {Engine}: {Results} results, block '{Block}', status {Status}, {Milliseconds} ms.")]
    private partial void LogSearched(WebSearchEngine engine, int results, string block, int status, long milliseconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Page read on {Host}: {Characters} characters, block '{Block}', status {Status}, {Milliseconds} ms.")]
    private partial void LogRead(string host, int characters, string block, int status, long milliseconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "The browser of Jarvis refused a request ({Why}).")]
    private partial void LogRefused(string why);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A host name could not be checked; its request was refused.")]
    private partial void LogResolveFailed(Exception exception);
}
