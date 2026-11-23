using System.Text;
using Kazo.Jarvis.Kernel.Files;

namespace Kazo.Jarvis.Adapters.Files;

/// <summary>
/// The files of the PC, read only (ADR 0017, section 3). It resolves a path the way Windows will follow it, so the
/// slice of the file tools compares real places (ADR 0009, section 5), and it never writes.
/// </summary>
public sealed class LocalFiles : ILocalFiles
{
    /// <summary>The largest file read: a text a model reads by parts is far smaller.</summary>
    public const long MostFileBytes = 5 * 1024 * 1024;

    /// <summary>How many entries of a folder are counted at most.</summary>
    private const int MostCounted = 10_000;

    /// <summary>How many links are followed for one path before it is given up: a loop of links never ends.</summary>
    private const int MostLinks = 40;

    /// <inheritdoc />
    public string? Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0', StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            path = StripLongPrefix(path.Trim());

            // « Documents\notes.txt » or « C:notes.txt » depend on a current folder a model does not know.
            if (!Path.IsPathFullyQualified(path))
            {
                return null;
            }

            return FollowLinks(Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            // What cannot be resolved cannot be compared: the caller treats it as outside every allowed folder.
            return null;
        }
    }

    /// <inheritdoc />
    public ValueTask<FolderListing> ListAsync(string folder, int most, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        try
        {
            if (File.Exists(folder))
            {
                throw new LocalFileException(LocalFileFailure.NotAFolder);
            }

            var directory = new DirectoryInfo(folder);
            if (!directory.Exists)
            {
                throw new LocalFileException(LocalFileFailure.NotFound);
            }

            var entries = new List<FolderEntry>();
            foreach (var entry in directory.EnumerateFileSystemInfos().Take(MostCounted))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var isFolder = (entry.Attributes & FileAttributes.Directory) != 0;
                entries.Add(new FolderEntry(entry.Name, isFolder, isFolder ? null : (entry as FileInfo)?.Length, entry.LastWriteTimeUtc));
            }

            var ordered = entries
                .OrderByDescending(entry => entry.IsFolder)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(0, most))
                .ToList();
            return ValueTask.FromResult(new FolderListing(ordered, entries.Count));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new LocalFileException(LocalFileFailure.AccessDenied, exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new LocalFileException(LocalFileFailure.NotFound, exception);
        }
        catch (IOException exception)
        {
            throw new LocalFileException(LocalFileFailure.Unreadable, exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<FileExcerpt> ReadTextAsync(string file, int start, int most, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        try
        {
            if (Directory.Exists(file))
            {
                throw new LocalFileException(LocalFileFailure.NotAFile);
            }

            var info = new FileInfo(file);
            if (!info.Exists)
            {
                throw new LocalFileException(LocalFileFailure.NotFound);
            }

            if (info.Length > MostFileBytes)
            {
                throw new LocalFileException(LocalFileFailure.TooLarge);
            }

            // Shared with writers: a file open in an editor is read all the same.
            byte[] bytes;
            await using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 16 * 1024, useAsync: true))
            {
                if (stream.Length > MostFileBytes)
                {
                    throw new LocalFileException(LocalFileFailure.TooLarge);
                }

                bytes = new byte[stream.Length];
                await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            }

            var text = Decode(bytes) ?? throw new LocalFileException(LocalFileFailure.NotText);
            start = Math.Clamp(start, 0, text.Length);
            var length = Math.Min(Math.Max(0, most), text.Length - start);
            return new FileExcerpt(text.Substring(start, length), start, text.Length);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new LocalFileException(LocalFileFailure.AccessDenied, exception);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new LocalFileException(LocalFileFailure.NotFound, exception);
        }
        catch (IOException exception)
        {
            throw new LocalFileException(LocalFileFailure.Unreadable, exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask AppendTextAsync(string file, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            if (Directory.Exists(file))
            {
                throw new LocalFileException(LocalFileFailure.NotAFile);
            }

            // UTF-8 without a mark: a mark in the middle of a file that exists would show as a stray character.
            await File.AppendAllTextAsync(file, text, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new LocalFileException(LocalFileFailure.AccessDenied, exception);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new LocalFileException(LocalFileFailure.NotFound, exception);
        }
        catch (IOException exception)
        {
            throw new LocalFileException(LocalFileFailure.Unreadable, exception);
        }
    }

    /// <summary>
    /// The text of <paramref name="bytes"/>, or <see langword="null"/> when they are not text: UTF-16 and UTF-8 by
    /// their mark, UTF-8 otherwise; a file that holds a zero byte or bytes UTF-8 does not have is not text.
    /// </summary>
    internal static string? Decode(byte[] bytes)
    {
        if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
        {
            var utf16 = bytes[0] == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode;
            return utf16.GetString(bytes, 2, bytes.Length - 2);
        }

        if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            return null;
        }

        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            // An old text file in the Windows code page: every byte is a character there.
            return Encoding.Latin1.GetString(bytes, offset, bytes.Length - offset);
        }
    }

    /// <summary>« \\?\C:\x » is « C:\x » and « \\?\UNC\server\share » is « \\server\share » : one spelling per place.</summary>
    private static string StripLongPrefix(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + path[8..];
        }

        return path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..] : path;
    }

    /// <summary>
    /// Replaces every link or junction on the way by its target, from the root down. What does not exist is kept as
    /// written: nothing can be read there, and the comparison still sees where it would be.
    /// </summary>
    private static string FollowLinks(string fullPath, int followed = 0)
    {
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var current = root;
        var parts = fullPath[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            current = Path.Combine(current, parts[index]);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (!info.Exists)
            {
                // The rest does not exist either.
                return Path.GetFullPath(Path.Combine([current, .. parts[(index + 1)..]]));
            }

            if (info.LinkTarget is null)
            {
                continue;
            }

            // A loop of links never ends: give up, and the path is treated as unusable.
            if (followed >= MostLinks)
            {
                throw new IOException("Too many links on the way.");
            }

            var target = info.ResolveLinkTarget(returnFinalTarget: true)
                ?? throw new IOException("A link on the way has no target.");

            // The target may itself sit under a link: start again from it, with what was left of the path.
            var next = StripLongPrefix(Path.GetFullPath(Path.Combine([target.FullName, .. parts[(index + 1)..]])));
            return FollowLinks(next, followed + 1);
        }

        return current.Length == 0 ? fullPath : current;
    }
}
