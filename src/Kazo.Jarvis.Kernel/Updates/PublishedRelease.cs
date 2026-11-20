namespace Kazo.Jarvis.Kernel.Updates;

/// <summary>One downloadable file of a published release (US1.18).</summary>
/// <param name="Name">File name, for instance <c>Kazo.Jarvis-0.2.0-win-x64.zip</c>.</param>
/// <param name="DownloadUrl">Where to download it.</param>
/// <param name="Size">Size in bytes, 0 when unknown.</param>
/// <param name="Digest">Checksum given by the release source, for instance <c>sha256:7f0b…</c>; <see langword="null"/> when none.</param>
public sealed record ReleasePackage(string Name, Uri DownloadUrl, long Size, string? Digest);

/// <summary>A release published on GitHub (US1.18). Plain data: the updates slice decides which one to install.</summary>
/// <param name="Tag">The release tag, for instance <c>v0.2.0</c>.</param>
/// <param name="IsDraft">A draft, never installed.</param>
/// <param name="IsPreRelease">Marked as a pre-release.</param>
/// <param name="PageUrl">The release page, to read its notes.</param>
/// <param name="Packages">Its downloadable files.</param>
public sealed record PublishedRelease(
    string Tag,
    bool IsDraft,
    bool IsPreRelease,
    Uri PageUrl,
    IReadOnlyList<ReleasePackage> Packages);
