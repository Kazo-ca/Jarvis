using System.Text;

namespace Kazo.Jarvis.Adapters.Files;

/// <summary>
/// Atomic writing (ADR 0004, section 2): a temporary file next to the target, flushed to disk, then moved
/// over it. A crash leaves either the old file or the new one, never half of one.
/// </summary>
internal static class AtomicFile
{
    /// <summary>Writes <paramref name="content"/> to <paramref name="path"/> as UTF-8 without a byte order mark.</summary>
    public static void WriteAllText(string path, string content)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// Copies <paramref name="source"/> into <paramref name="backupFolder"/> as
    /// <c>&lt;name&gt;.&lt;reason&gt;.&lt;yyyyMMdd-HHmmss&gt;&lt;extension&gt;</c>, never over an existing copy.
    /// </summary>
    /// <returns>The path of the copy.</returns>
    public static string Backup(string source, string backupFolder, string reason, DateTimeOffset now)
    {
        Directory.CreateDirectory(backupFolder);
        var target = BackupPath(source, backupFolder, reason, now);
        File.Copy(source, target, overwrite: false);
        return target;
    }

    /// <summary>A free path for a copy of <paramref name="source"/> (see <see cref="Backup"/>).</summary>
    public static string BackupPath(string source, string backupFolder, string reason, DateTimeOffset now)
    {
        var name = Path.GetFileNameWithoutExtension(source);
        var extension = Path.GetExtension(source);
        var stamp = now.UtcDateTime.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var target = Path.Combine(backupFolder, $"{name}.{reason}.{stamp}{extension}");
        for (var counter = 2; File.Exists(target); counter++)
        {
            target = Path.Combine(backupFolder, $"{name}.{reason}.{stamp}-{counter}{extension}");
        }

        return target;
    }
}
