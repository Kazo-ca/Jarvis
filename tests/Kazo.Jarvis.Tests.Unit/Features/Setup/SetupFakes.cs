using Kazo.Jarvis.Kernel.Desktop;
using Kazo.Jarvis.Kernel.Hardware;
using Kazo.Jarvis.Kernel.Models;
using Microsoft.Extensions.Localization;

namespace Kazo.Jarvis.Tests.Unit.Features.Setup;

/// <summary>An installer in memory: what it installed, which targets fail, and a gate to hold a download.</summary>
internal sealed class FakeModelInstaller : IModelInstaller
{
    public HashSet<string> Installed { get; } = new(StringComparer.Ordinal);

    public List<string> Downloaded { get; } = [];

    public Dictionary<string, ModelDownloadFailure> Failures { get; } = new(StringComparer.Ordinal);

    public TaskCompletionSource? Hold { get; set; }

    public bool IsInstalled(ModelDownload download) => Installed.Contains(download.Target);

    public async Task InstallAsync(ModelDownload download, IProgress<long>? progress, CancellationToken cancellationToken = default)
    {
        Downloaded.Add(download.Target);
        progress?.Report(download.Size / 2);
        if (Hold is { } hold)
        {
            await hold.Task.WaitAsync(cancellationToken);
        }

        if (Failures.TryGetValue(download.Target, out var failure))
        {
            throw new ModelDownloadException(failure, "fake failure");
        }

        progress?.Report(download.Size);
        Installed.Add(download.Target);
    }
}

internal sealed class FakeGraphicsCardProbe(GraphicsCard? card) : IGraphicsCardProbe
{
    public Task<GraphicsCard?> FindNvidiaCardAsync(CancellationToken cancellationToken = default) => Task.FromResult(card);
}

internal sealed class RecordingDialogs : IDialogService
{
    private readonly List<string> open = [];

    public List<DialogRequest> Opened { get; } = [];

    // No test here closes a window.
    public event Action<string>? Closed
    {
        add { }
        remove { }
    }

    public void Open(DialogRequest request)
    {
        Opened.Add(request);
        if (!open.Contains(request.Key))
        {
            open.Add(request.Key);
        }
    }

    public void Close(string key) => open.Remove(key);

    public bool IsOpen(string key) => open.Contains(key);
}

/// <summary>Gives the key back as the text.</summary>
internal sealed class KeyLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] => new(name, name);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
