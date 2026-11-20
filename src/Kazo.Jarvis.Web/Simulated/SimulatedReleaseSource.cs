using Kazo.Jarvis.Kernel.Updates;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="IReleaseSource"/> (US1.18): no network. Buttons of the page publish a version or cut
/// the connection; the download takes a few hundred milliseconds and reports its progress.
/// </summary>
public sealed class SimulatedReleaseSource : IReleaseSource
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(150);
    private readonly Lock gate = new();
    private readonly List<PublishedRelease> releases = [];

    /// <summary>Whether GitHub answers. When <see langword="false"/>, every call fails as without a network.</summary>
    public bool Reachable { get; set; } = true;

    /// <summary>The versions published so far, newest first.</summary>
    public IReadOnlyList<PublishedRelease> Releases
    {
        get
        {
            lock (gate)
            {
                return [.. releases];
            }
        }
    }

    /// <summary>Publishes a pre-release <paramref name="version"/> with its package.</summary>
    /// <param name="version">The version, for instance <c>9.9.0</c>.</param>
    public void Publish(string version)
    {
        var package = new ReleasePackage($"Kazo.Jarvis-{version}-win-x64.zip", new Uri($"https://example.invalid/Kazo.Jarvis-{version}-win-x64.zip"), 1_000_000, null);
        var release = new PublishedRelease($"v{version}", false, true, new Uri($"https://github.com/Kazo-ca/Jarvis/releases/tag/v{version}"), [package]);
        lock (gate)
        {
            releases.RemoveAll(r => r.Tag == release.Tag);
            releases.Insert(0, release);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PublishedRelease>> GetReleasesAsync(CancellationToken cancellationToken = default) =>
        Reachable
            ? Task.FromResult(Releases)
            : Task.FromException<IReadOnlyList<PublishedRelease>>(new HttpRequestException("Simulated: no network."));

    /// <inheritdoc />
    public async Task<string> PrepareAsync(ReleasePackage package, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        foreach (var fraction in new[] { 0.25, 0.5, 0.75, 1.0 })
        {
            if (!Reachable)
            {
                throw new HttpRequestException("Simulated: the download was cut.");
            }

            await Task.Delay(Step, cancellationToken).ConfigureAwait(false);
            progress?.Report(fraction);
        }

        return $"simulated/updates/{Path.GetFileNameWithoutExtension(package.Name)}/content";
    }
}
