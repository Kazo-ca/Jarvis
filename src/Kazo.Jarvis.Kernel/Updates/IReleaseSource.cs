namespace Kazo.Jarvis.Kernel.Updates;

/// <summary>
/// Where releases are published (US1.18, ADR 0001, section 3): the public GitHub releases of
/// <c>Kazo-ca/Jarvis</c>, read without a token. The Windows host uses the real adapter; the Web host a
/// simulated list. Only called when the user asked for it, or when the automatic check is on.
/// </summary>
public interface IReleaseSource
{
    /// <summary>The most recent releases, newest first, drafts included as the source returns them.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The releases.</returns>
    /// <exception cref="HttpRequestException">No network, or the source refused (GitHub rate limit, for instance).</exception>
    Task<IReadOnlyList<PublishedRelease>> GetReleasesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads <paramref name="package"/>, checks its digest and unpacks it in a folder of its own, ready
    /// for the updater. The application keeps running meanwhile.
    /// </summary>
    /// <param name="package">The package to prepare.</param>
    /// <param name="progress">Receives the downloaded fraction, from 0 to 1, when the size is known.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The folder holding the unpacked new version, updater included.</returns>
    /// <exception cref="HttpRequestException">The download failed.</exception>
    /// <exception cref="InvalidDataException">The file does not match its digest, or is not a Kazo.Jarvis package.</exception>
    Task<string> PrepareAsync(ReleasePackage package, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
