using System.Windows;
using System.Windows.Threading;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Desktop;

namespace Kazo.Jarvis.Windows.Dialogs;

/// <summary><see cref="IDialogService"/> of the Windows host: one <see cref="DialogWindow"/> per key.</summary>
internal sealed class WindowDialogService : IDialogService
{
    private readonly IServiceProvider services;
    private readonly Dispatcher dispatcher;
    private readonly JarvisPaths paths;

    // Only touched on the UI thread.
    private readonly Dictionary<string, DialogWindow> open = new(StringComparer.Ordinal);

    /// <summary>Creates the service.</summary>
    public WindowDialogService(IServiceProvider services, Dispatcher dispatcher, JarvisPaths paths)
    {
        this.services = services;
        this.dispatcher = dispatcher;
        this.paths = paths;
    }

    /// <inheritdoc />
    public void Open(DialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        dispatcher.Invoke(() =>
        {
            if (open.TryGetValue(request.Key, out var existing))
            {
                if (existing.WindowState == WindowState.Minimized)
                {
                    existing.WindowState = WindowState.Normal;
                }

                existing.Activate();
                return;
            }

            var window = new DialogWindow(services, request, paths.WebView);
            window.Closed += (_, _) => open.Remove(request.Key);
            open[request.Key] = window;
            window.Show();
        });
    }

    /// <inheritdoc />
    public void Close(string key) => dispatcher.Invoke(() =>
    {
        if (open.TryGetValue(key, out var window))
        {
            window.Close();
        }
    });

    /// <inheritdoc />
    public bool IsOpen(string key) => dispatcher.Invoke(() => open.ContainsKey(key));

    /// <summary>Closes every dialog (the application quits).</summary>
    public void CloseAll() => dispatcher.Invoke(() =>
    {
        foreach (var window in open.Values.ToList())
        {
            window.Close();
        }
    });
}
