using Kazo.Jarvis.Kernel.Updates;

namespace Kazo.Jarvis.Features.Maintenance.Updates;

/// <summary>A release newer than the running version, with the package to install.</summary>
/// <param name="Release">The release.</param>
/// <param name="Version">Its version, read from the tag.</param>
/// <param name="Package">The package for this PC.</param>
public sealed record AvailableUpdate(PublishedRelease Release, ReleaseVersion Version, ReleasePackage Package);
