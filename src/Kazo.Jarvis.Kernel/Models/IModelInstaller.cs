namespace Kazo.Jarvis.Kernel.Models;

/// <summary>
/// Puts models in the models folder (ADR 0006, section 7): the Windows host downloads them, checks their checksum
/// and unpacks the archives; the Web host simulates it. Slices never know the folder nor the network.
/// </summary>
public interface IModelInstaller
{
    /// <summary>Whether <see cref="ModelDownload.Target"/> is already in the models folder.</summary>
    /// <param name="download">The download.</param>
    /// <returns><see langword="true"/> when the file, or the folder of an archive, is there.</returns>
    bool IsInstalled(ModelDownload download);

    /// <summary>
    /// Downloads, checks and puts the file in place. A partial or wrong file never takes the place of the
    /// target: it is written aside and deleted on failure.
    /// </summary>
    /// <param name="download">What to download.</param>
    /// <param name="progress">Receives the number of bytes received so far; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the download; nothing is left behind.</param>
    /// <returns>A task that completes once the model is in place.</returns>
    /// <exception cref="ModelDownloadException">The download failed, with its cause.</exception>
    Task InstallAsync(ModelDownload download, IProgress<long>? progress, CancellationToken cancellationToken = default);
}
