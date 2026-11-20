using System.Formats.Tar;
using System.Security.Cryptography;
using ICSharpCode.SharpZipLib.BZip2;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Adapters.Models;

/// <summary>
/// Downloads the models into <c>%USERPROFILE%\.kazo\jarvis\models</c> (ADR 0006, section 7). The file is written
/// to <c>models\.downloads</c> while it arrives, its size and checksum are checked, then it is moved into place
/// (or unpacked there, for the <c>.tar.bz2</c> voices of sherpa-onnx). A wrong or partial file never takes the
/// place of a model.
/// </summary>
public sealed partial class HttpModelInstaller : IModelInstaller
{
    /// <summary>Name of the folder, in the models folder, where downloads wait for their check.</summary>
    public const string StagingFolderName = ".downloads";

    private const int BufferSize = 1 << 16;
    private const int DiskFullHResult = unchecked((int)0x80070070);
    private const int DiskFullHResultLinux = 28;

    private readonly HttpClient client;
    private readonly string modelsFolder;
    private readonly ILogger logger;

    /// <summary>Creates the installer.</summary>
    /// <param name="client">The client; its timeout must allow a download of several hundred megabytes.</param>
    /// <param name="paths">The folders of Jarvis.</param>
    /// <param name="logger">Logs the downloads; none when omitted.</param>
    public HttpModelInstaller(HttpClient client, JarvisPaths paths, ILogger<HttpModelInstaller>? logger = null)
        : this(client, (paths ?? throw new ArgumentNullException(nameof(paths))).Models, logger)
    {
    }

    /// <summary>Creates the installer on a given models folder (tests).</summary>
    /// <param name="client">The client.</param>
    /// <param name="modelsFolder">The models folder.</param>
    /// <param name="logger">Logs the downloads; none when omitted.</param>
    internal HttpModelInstaller(HttpClient client, string modelsFolder, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(modelsFolder);
        this.client = client;
        this.modelsFolder = modelsFolder;
        this.logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public bool IsInstalled(ModelDownload download)
    {
        ArgumentNullException.ThrowIfNull(download);
        var target = TargetPath(download);
        return download.Format == ModelDownloadFormat.File
            ? File.Exists(target)
            : Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any();
    }

    /// <inheritdoc />
    public async Task InstallAsync(ModelDownload download, IProgress<long>? progress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(download);
        var target = TargetPath(download);
        var (algorithm, expected) = ParseChecksum(download.Checksum);
        var staging = Path.Combine(modelsFolder, StagingFolderName);
        var stem = Path.Combine(staging, Guid.NewGuid().ToString("N"));
        var partial = stem + ".part";
        var unpacked = stem;
        LogStarting(download.Source, download.Target, download.Size);

        try
        {
            Directory.CreateDirectory(staging);
            await DownloadAsync(download, partial, algorithm, expected, progress, cancellationToken).ConfigureAwait(false);

            if (download.Format == ModelDownloadFormat.File)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(partial, target, overwrite: true);
            }
            else
            {
                await UnpackAsync(partial, unpacked, download.Target, cancellationToken).ConfigureAwait(false);
                if (Directory.Exists(target))
                {
                    Directory.Delete(target, recursive: true);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                Directory.Move(Path.Combine(unpacked, download.Target), target);
            }

            LogInstalled(download.Target);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ModelDownloadException exception)
        {
            LogFailed(exception.Failure, download.Target, exception);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var failure = IsDiskFull(exception) || exception is UnauthorizedAccessException ? ModelDownloadFailure.DiskError : ModelDownloadFailure.Failed;
            LogFailed(failure, download.Target, exception);
            throw new ModelDownloadException(failure, $"{download.Target} could not be written: {exception.Message}", exception);
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException or BZip2Exception)
        {
            LogFailed(ModelDownloadFailure.Failed, download.Target, exception);
            throw new ModelDownloadException(ModelDownloadFailure.Failed, $"{download.Target} could not be unpacked: {exception.Message}", exception);
        }
        finally
        {
            TryDelete(partial);
            TryDeleteFolder(unpacked);
        }
    }

    /// <summary>The full path of a target, refusing anything that would leave the models folder.</summary>
    internal string TargetPath(ModelDownload download)
    {
        var relative = download.Target.Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)
            || relative.Split(Path.DirectorySeparatorChar).Any(part => part is "" or "." or ".."))
        {
            throw new ArgumentException($"'{download.Target}' is not a path inside the models folder.", nameof(download));
        }

        return Path.Combine(modelsFolder, relative);
    }

    /// <summary>Reads <c>sha256:&lt;hex&gt;</c> or <c>sha1:&lt;hex&gt;</c>.</summary>
    internal static (HashAlgorithmName Algorithm, byte[] Expected) ParseChecksum(string checksum)
    {
        ArgumentNullException.ThrowIfNull(checksum);
        var separator = checksum.IndexOf(':', StringComparison.Ordinal);
        var name = separator > 0 ? checksum[..separator].ToLowerInvariant() : string.Empty;
        var (algorithm, length) = name switch
        {
            "sha256" => (HashAlgorithmName.SHA256, 32),
            "sha1" => (HashAlgorithmName.SHA1, 20),
            _ => throw new ArgumentException($"'{checksum}' is not a sha256: or sha1: checksum.", nameof(checksum)),
        };

        var bytes = Convert.FromHexString(checksum[(separator + 1)..]);
        return bytes.Length == length ? (algorithm, bytes) : throw new ArgumentException($"'{checksum}' has the wrong length.", nameof(checksum));
    }

    private async Task DownloadAsync(
        ModelDownload download,
        string partial,
        HashAlgorithmName algorithm,
        byte[] expected,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(download.Source, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new ModelDownloadException(ModelDownloadFailure.Unreachable, $"{download.Source.Host} could not be reached: {exception.Message}", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ModelDownloadException(ModelDownloadFailure.Unreachable, $"{download.Source.Host} did not answer in time.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ModelDownloadException(ModelDownloadFailure.Unreachable, $"{download.Source} answered {(int)response.StatusCode}.");
            }

            using var hash = IncrementalHash.CreateHash(algorithm);
            long received = 0;
            var buffer = new byte[BufferSize];
            try
            {
                var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (source.ConfigureAwait(false))
                {
                    var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
                    await using (file.ConfigureAwait(false))
                    {
                        int read;
                        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            hash.AppendData(buffer, 0, read);
                            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                            received += read;
                            progress?.Report(received);
                        }
                    }
                }
            }
            catch (HttpRequestException exception)
            {
                throw new ModelDownloadException(ModelDownloadFailure.Unreachable, $"The download of {download.Target} was cut: {exception.Message}", exception);
            }
            catch (IOException exception) when (!IsDiskFull(exception) && exception is not FileNotFoundException and not DirectoryNotFoundException)
            {
                // A reset connection surfaces as an IOException of the response stream.
                throw new ModelDownloadException(ModelDownloadFailure.Unreachable, $"The download of {download.Target} was cut: {exception.Message}", exception);
            }

            if (received != download.Size)
            {
                throw new ModelDownloadException(ModelDownloadFailure.ChecksumMismatch, $"{download.Target}: {received} bytes received, {download.Size} expected.");
            }

            var actual = hash.GetHashAndReset();
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                throw new ModelDownloadException(
                    ModelDownloadFailure.ChecksumMismatch,
                    $"{download.Target}: checksum {Convert.ToHexStringLower(actual)} instead of {Convert.ToHexStringLower(expected)}.");
            }
        }
    }

    private static async Task UnpackAsync(string archive, string folder, string expectedTop, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(folder);
        var file = File.OpenRead(archive);
        await using (file.ConfigureAwait(false))
        {
            var bzip = new BZip2InputStream(file) { IsStreamOwner = false };
            await using (bzip.ConfigureAwait(false))
            {
                // TarFile refuses entries that would land outside the folder.
                await TarFile.ExtractToDirectoryAsync(bzip, folder, overwriteFiles: true, cancellationToken).ConfigureAwait(false);
            }
        }

        if (!Directory.Exists(Path.Combine(folder, expectedTop)))
        {
            throw new InvalidDataException($"The archive does not hold the folder '{expectedTop}'.");
        }
    }

    private static bool IsDiskFull(Exception exception) =>
        exception is IOException { HResult: DiskFullHResult or DiskFullHResultLinux };

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading {Source} to models/{Target} ({Size} bytes).")]
    private partial void LogStarting(Uri source, string target, long size);

    [LoggerMessage(Level = LogLevel.Information, Message = "Model models/{Target} installed.")]
    private partial void LogInstalled(string target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Model download failed ({Failure}): models/{Target}.")]
    private partial void LogFailed(ModelDownloadFailure failure, string target, Exception exception);
}
