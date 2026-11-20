using Kazo.Jarvis.Kernel.Updates;

namespace Kazo.Jarvis.Tests.Unit.Features.Maintenance.Updates;

internal sealed class FakeReleaseSource : IReleaseSource
{
    public List<PublishedRelease> Releases { get; } = [];

    public Exception? CheckFailure { get; set; }

    public Exception? PrepareFailure { get; set; }

    public int Checks { get; private set; }

    public List<ReleasePackage> Prepared { get; } = [];

    public TaskCompletionSource? HoldCheck { get; set; }

    public static PublishedRelease Release(string tag, bool preRelease = false, bool draft = false, string? packageName = null)
    {
        var version = tag.TrimStart('v');
        var package = new ReleasePackage(packageName ?? $"Kazo.Jarvis-{version}-win-x64.zip", new Uri($"https://example.invalid/{version}.zip"), 100, null);
        return new PublishedRelease(tag, draft, preRelease, new Uri($"https://example.invalid/releases/{tag}"), [package]);
    }

    public async Task<IReadOnlyList<PublishedRelease>> GetReleasesAsync(CancellationToken cancellationToken = default)
    {
        Checks++;
        if (HoldCheck is not null)
        {
            await HoldCheck.Task.WaitAsync(cancellationToken);
        }

        if (CheckFailure is not null)
        {
            throw CheckFailure;
        }

        return [.. Releases];
    }

    public Task<string> PrepareAsync(ReleasePackage package, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        Prepared.Add(package);
        if (PrepareFailure is not null)
        {
            return Task.FromException<string>(PrepareFailure);
        }

        progress?.Report(0.5);
        progress?.Report(1);
        return Task.FromResult($"prepared/{package.Name}");
    }
}

internal sealed class FakeUpdateLauncher : IUpdateLauncher
{
    public string CurrentVersion { get; set; } = "0.1.0+abcdef";

    public bool CanInstall { get; set; } = true;

    public bool Succeeds { get; set; } = true;

    public List<string> Launched { get; } = [];

    public bool Launch(string preparedFolder)
    {
        Launched.Add(preparedFolder);
        return Succeeds;
    }
}
