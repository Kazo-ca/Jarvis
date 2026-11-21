using System.Windows;

namespace Spikes.WebViewSearch;

/// <summary>
/// E15 spike: web search through a WebView2 control hosted in a never-activated WPF window.
/// Usage: WebViewSearch.exe probe | security | search [--shown] [--invisible] [--label x] [--engines a,b]
///        | profile-set | profile-clear | profile-check   [--root C:\path]
/// </summary>
internal static class Program
{
    private const string DefaultRoot = @"C:\jarvis\temp\e15-webview-search";

    [STAThread]
    private static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            var code = 1;
            try
            {
                code = await RunAsync(args);
            }
            catch (Exception ex)
            {
                Log.Line("FATAL " + ex);
            }
            finally
            {
                app.Shutdown(code);
            }
        };
        return app.Run();
    }

    private static async Task<int> RunAsync(string[] args)
    {
        var mode = args.FirstOrDefault() ?? "probe";
        var paths = new Paths(Option(args, "--root") ?? DefaultRoot);
        paths.Ensure();
        Log.Init(paths, mode);
        Log.Line($"start {string.Join(' ', args)}");
        var code = mode switch
        {
            "probe" => await Scenarios.ProbeAsync(paths),
            "security" => await Security.RunAsync(paths),
            "search" => await Scenarios.SearchAsync(paths,
                new HostOptions(args.Contains("--shown") ? WindowMode.ShownNoActivate : WindowMode.NeverShown, !args.Contains("--invisible")),
                Option(args, "--label") ?? "main",
                (Option(args, "--engines") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries),
                int.Parse(Option(args, "--queries") ?? "10")),
            "diag" => await Scenarios.DiagAsync(paths, (Option(args, "--engines") ?? "google,bing").Split(',')),
            "profile-set" => await Scenarios.ProfileAsync(paths, "set"),
            "profile-clear" => await Scenarios.ProfileAsync(paths, "clear"),
            "profile-check" => await Scenarios.ProfileAsync(paths, "check"),
            _ => 64,
        };
        Log.Line($"end code={code}");
        return code;
    }

    private static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
