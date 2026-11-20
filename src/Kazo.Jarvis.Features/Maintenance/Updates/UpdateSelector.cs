using Kazo.Jarvis.Kernel.Updates;

namespace Kazo.Jarvis.Features.Maintenance.Updates;

/// <summary>Picks the release to install among the published ones (US1.18).</summary>
public static class UpdateSelector
{
    /// <summary>Start of the package name: <c>Kazo.Jarvis-&lt;version&gt;-win-x64.zip</c> (see <c>build/Publish-Release.ps1</c>).</summary>
    public const string PackagePrefix = "Kazo.Jarvis-";

    /// <summary>End of the package name. Kazo.Jarvis targets x64 only (ADR 0001, section 1).</summary>
    public const string PackageSuffix = "-win-x64.zip";

    /// <summary>
    /// The newest release that is not a draft, is newer than <paramref name="current"/> and has a package for
    /// this PC; <see langword="null"/> when the running version is the newest.
    /// </summary>
    /// <param name="releases">The published releases, in any order.</param>
    /// <param name="current">The running version; <see langword="null"/> when unknown, which accepts any release.</param>
    /// <param name="includePreReleases">Whether pre-releases count.</param>
    /// <returns>The update to offer, or <see langword="null"/>.</returns>
    public static AvailableUpdate? SelectNewest(IEnumerable<PublishedRelease> releases, ReleaseVersion? current, bool includePreReleases)
    {
        ArgumentNullException.ThrowIfNull(releases);
        AvailableUpdate? best = null;
        foreach (var release in releases)
        {
            if (release.IsDraft || (release.IsPreRelease && !includePreReleases))
            {
                continue;
            }

            if (!ReleaseVersion.TryParse(release.Tag, out var version)
                || (current is not null && !(version > current))
                || (best is not null && !(version > best.Version)))
            {
                continue;
            }

            var package = release.Packages.FirstOrDefault(IsPackage);
            if (package is not null)
            {
                best = new AvailableUpdate(release, version, package);
            }
        }

        return best;
    }

    private static bool IsPackage(ReleasePackage package) =>
        package.Name.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase)
        && package.Name.EndsWith(PackageSuffix, StringComparison.OrdinalIgnoreCase);
}
