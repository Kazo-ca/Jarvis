using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Updates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Adapters.Updates;

/// <summary>
/// <see cref="IReleaseSource"/> over the public GitHub releases of <c>Kazo-ca/Jarvis</c> (US1.18). The
/// repository is public (ADR 0014): no token. Downloads go to <see cref="JarvisPaths.Updates"/>, are checked
/// against GitHub's SHA-256 digest, then unpacked. Copied from Kazo Bar's update client (ADR 0014).
/// </summary>
public sealed partial class GitHubReleaseSource : IReleaseSource
{
    /// <summary>Owner of the repository whose releases are read.</summary>
    public const string Owner = "Kazo-ca";

    /// <summary>The repository whose releases are read.</summary>
    public const string Repository = "Jarvis";

    /// <summary>The application, which a package must hold.</summary>
    public const string ApplicationExecutable = "Kazo.Jarvis.Windows.exe";

    /// <summary>The updater, which a package must hold: it is started from the unpacked folder.</summary>
    public const string UpdaterExecutable = "Kazo.Jarvis.Updater.exe";

    private const int PageSize = 30;
    private const int BufferSize = 81920;
    private const string UserAgent = "Kazo-Jarvis-Updates";

    private readonly HttpClient http;
    private readonly JarvisPaths paths;
    private readonly ILogger<GitHubReleaseSource> logger;

    /// <summary>Creates the source.</summary>
    /// <param name="http">The HTTP client; tests give one over a fake handler.</param>
    /// <param name="paths">Where downloads go.</param>
    /// <param name="logger">Logs downloads and clean-ups.</param>
    public GitHubReleaseSource(HttpClient http, JarvisPaths paths, ILogger<GitHubReleaseSource>? logger = null)
    {
        this.http = http;
        this.paths = paths;
        this.logger = logger ?? NullLogger<GitHubReleaseSource>.Instance;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PublishedRelease>> GetReleasesAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Owner}/{Repository}/releases?per_page={PageSize}");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return Parse(document.RootElement);
        }
        catch (JsonException exception)
        {
            // Treated like an unreachable source: the slice shows "GitHub could not be reached".
            throw new HttpRequestException("GitHub returned an answer that is not a list of releases.", exception);
        }
    }

    /// <inheritdoc />
    public async Task<string> PrepareAsync(ReleasePackage package, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        var name = SafeName(Path.GetFileNameWithoutExtension(package.Name));
        var folder = Path.Combine(paths.Updates, name);
        DeleteQuietly(folder);
        Directory.CreateDirectory(folder);

        var zip = Path.Combine(folder, "package.zip");
        await DownloadAsync(package, zip, progress, cancellationToken).ConfigureAwait(false);

        var content = Path.Combine(folder, "content");
        await Task.Run(() => Extract(zip, content), cancellationToken).ConfigureAwait(false);
        File.Delete(zip);
        LogPrepared(package.Name, content);
        return content;
    }

    /// <summary>Deletes what earlier updates left in <see cref="JarvisPaths.Updates"/>. A folder still in use stays until next time.</summary>
    public void DeleteLeftovers()
    {
        if (!Directory.Exists(paths.Updates))
        {
            return;
        }

        foreach (var folder in Directory.GetDirectories(paths.Updates))
        {
            if (DeleteQuietly(folder))
            {
                LogDeleted(folder);
            }
        }
    }

    /// <summary>Reads the JSON list of releases of the GitHub API.</summary>
    internal static IReadOnlyList<PublishedRelease> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Expected a JSON array of releases.");
        }

        var releases = new List<PublishedRelease>();
        foreach (var item in root.EnumerateArray())
        {
            var tag = GetString(item, "tag_name");
            if (tag is null || !Uri.TryCreate(GetString(item, "html_url"), UriKind.Absolute, out var page))
            {
                continue;
            }

            var packages = new List<ReleasePackage>();
            if (item.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var assetName = GetString(asset, "name");
                    if (assetName is null || !Uri.TryCreate(GetString(asset, "browser_download_url"), UriKind.Absolute, out var url))
                    {
                        continue;
                    }

                    var size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var bytes) ? bytes : 0;
                    packages.Add(new ReleasePackage(assetName, url, size, GetString(asset, "digest")));
                }
            }

            releases.Add(new PublishedRelease(tag, GetBool(item, "draft"), GetBool(item, "prerelease"), page, packages));
        }

        return releases;
    }

    /// <summary>
    /// Checks a <c>sha256:&lt;hex&gt;</c> digest. A missing digest or another algorithm is accepted: there is
    /// nothing to compare with (GitHub gives SHA-256 for every asset uploaded since 2025).
    /// </summary>
    /// <exception cref="InvalidDataException">The file does not match.</exception>
    internal static void VerifyDigest(string file, string? digest)
    {
        const string Prefix = "sha256:";
        if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var expected = digest[Prefix.Length..].Trim();
        using var stream = File.OpenRead(file);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"The downloaded file is damaged (SHA-256 {actual.ToLowerInvariant()}, expected {expected}).");
        }
    }

    /// <summary>Unpacks the package and checks it holds the application and the updater at its root.</summary>
    /// <exception cref="InvalidDataException">Not a zip, or not a Kazo.Jarvis package.</exception>
    internal static void Extract(string zip, string content)
    {
        DeleteQuietly(content);
        Directory.CreateDirectory(content);
        try
        {
            // ExtractToDirectory refuses entries that would land outside the folder ("zip slip").
            ZipFile.ExtractToDirectory(zip, content);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("The package could not be unpacked.", exception);
        }

        foreach (var required in new[] { ApplicationExecutable, UpdaterExecutable })
        {
            if (!File.Exists(Path.Combine(content, required)))
            {
                throw new InvalidDataException($"The package does not hold {required} at its root.");
            }
        }
    }

    private async Task DownloadAsync(ReleasePackage package, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var partial = destination + ".part";
        using (var request = new HttpRequestMessage(HttpMethod.Get, package.DownloadUrl))
        {
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1.0"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? package.Size;

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
            var buffer = new byte[BufferSize];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                received += read;
                if (total > 0)
                {
                    progress?.Report(Math.Min(1.0, (double)received / total));
                }
            }
        }

        try
        {
            VerifyDigest(partial, package.Digest);
        }
        catch
        {
            DeleteFileQuietly(partial);
            throw;
        }

        File.Move(partial, destination, overwrite: true);
        progress?.Report(1.0);
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string SafeName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static bool DeleteQuietly(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void DeleteFileQuietly(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Package {Package} downloaded, checked and unpacked in {Folder}.")]
    private partial void LogPrepared(string package, string folder);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted the leftovers of an earlier update: {Folder}.")]
    private partial void LogDeleted(string folder);
}
