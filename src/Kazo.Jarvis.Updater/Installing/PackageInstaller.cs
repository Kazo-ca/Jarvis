namespace Kazo.Jarvis.Updater.Installing;

/// <summary>
/// Replaces the files of an installation folder with those of a new version (US1.18). Copied from Kazo
/// Bar's installer (ADR 0014). Every current file is first <b>moved</b> into a backup folder inside the
/// installation folder (same volume, so a move always works), then the new files are copied in. Any failure
/// moves the old files back. The user's data (<c>%USERPROFILE%\.kazo\jarvis</c>) lives elsewhere and is
/// never read or written; <see cref="EnsureSafe"/> refuses a folder that overlaps it.
/// </summary>
internal sealed class PackageInstaller
{
    /// <summary>
    /// Backups are named <c>.kazo-update-backup-yyyyMMddHHmmss</c>. The application deletes them at its next
    /// start; the updater deletes older ones before an update.
    /// </summary>
    public const string BackupPrefix = ".kazo-update-backup";

    /// <summary>The application, which both folders must hold.</summary>
    public const string ApplicationExecutable = "Kazo.Jarvis.Windows.exe";

    private readonly Action<string> log;

    /// <summary>Creates the installer.</summary>
    /// <param name="log">Receives one line per step.</param>
    public PackageInstaller(Action<string> log) => this.log = log;

    /// <summary>
    /// Refuses folders where moving every file away would be dangerous: a drive root, a folder without the
    /// application, the source itself, or a folder overlapping the user data folder.
    /// </summary>
    /// <param name="installFolder">The folder to update.</param>
    /// <param name="sourceFolder">The unpacked new version.</param>
    /// <param name="userDataFolder">The user's data, never touched.</param>
    /// <exception cref="InvalidOperationException">The folders are not safe to update.</exception>
    public static void EnsureSafe(string installFolder, string sourceFolder, string userDataFolder)
    {
        var install = Normalize(installFolder);
        var source = Normalize(sourceFolder);
        var userData = Normalize(userDataFolder);

        if (Path.GetPathRoot(install) is { } root && string.Equals(Normalize(root), install, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to update a drive root ({installFolder}).");
        }

        if (!File.Exists(Path.Combine(install, ApplicationExecutable)))
        {
            throw new InvalidOperationException($"{installFolder} is not a Kazo.Jarvis folder ({ApplicationExecutable} not found).");
        }

        if (!File.Exists(Path.Combine(source, ApplicationExecutable)))
        {
            throw new InvalidOperationException($"{sourceFolder} does not hold a new version ({ApplicationExecutable} not found).");
        }

        if (IsSameOrInside(install, source) || IsSameOrInside(source, install))
        {
            throw new InvalidOperationException($"The new version {sourceFolder} overlaps the installation folder {installFolder}.");
        }

        if (IsSameOrInside(install, userData) || IsSameOrInside(userData, install))
        {
            throw new InvalidOperationException($"{installFolder} overlaps the user data folder {userDataFolder}; refusing to update it.");
        }
    }

    /// <summary>Installs the files of <paramref name="sourceFolder"/> into <paramref name="installFolder"/>, rolling back on failure.</summary>
    /// <param name="sourceFolder">The unpacked new version.</param>
    /// <param name="installFolder">The folder to update.</param>
    /// <param name="now">The time, for the backup name.</param>
    /// <returns>The backup folder holding the previous version.</returns>
    public string Apply(string sourceFolder, string installFolder, DateTime now)
    {
        var install = Normalize(installFolder);
        var source = Normalize(sourceFolder);
        var backup = Path.Combine(install, $"{BackupPrefix}-{now:yyyyMMddHHmmss}");
        var moved = new List<(string Original, string Backup)>();
        var copied = new List<string>();

        try
        {
            foreach (var file in EnumerateInstalledFiles(install))
            {
                var target = Path.Combine(backup, Path.GetRelativePath(install, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(file, target);
                moved.Add((file, target));
            }

            log($"Moved {moved.Count} file(s) of the current version to {backup}.");

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(install, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: false);
                copied.Add(target);
            }

            log($"Copied {copied.Count} file(s) of the new version.");
        }
        catch (Exception exception)
        {
            log($"Install failed, restoring the previous version: {exception}");
            Rollback(moved, copied);
            throw;
        }

        DeleteEmptyFolders(install);
        return backup;
    }

    /// <summary>Deletes the backups of earlier updates. One still in use stays until the next time.</summary>
    /// <param name="installFolder">The installation folder.</param>
    /// <param name="log">Receives one line per backup.</param>
    public static void DeleteBackups(string installFolder, Action<string> log)
    {
        if (!Directory.Exists(installFolder))
        {
            return;
        }

        foreach (var folder in Directory.GetDirectories(installFolder, BackupPrefix + "*"))
        {
            try
            {
                Directory.Delete(folder, recursive: true);
                log($"Deleted old update backup {folder}.");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                log($"Old update backup {folder} still in use; kept for now.");
            }
        }
    }

    private void Rollback(List<(string Original, string Backup)> moved, List<string> copied)
    {
        foreach (var file in copied)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                log($"Rollback: could not delete {file}: {exception.Message}");
            }
        }

        for (var i = moved.Count - 1; i >= 0; i--)
        {
            var (original, backup) = moved[i];
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(original)!);
                File.Move(backup, original, overwrite: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                log($"Rollback: could not restore {original} from {backup}: {exception.Message}");
            }
        }
    }

    private static List<string> EnumerateInstalledFiles(string install) =>
        Directory.EnumerateFiles(install, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(install, file).StartsWith(BackupPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static void DeleteEmptyFolders(string install)
    {
        foreach (var folder in Directory.GetDirectories(install, "*", SearchOption.AllDirectories).OrderByDescending(f => f.Length))
        {
            if (Path.GetRelativePath(install, folder).StartsWith(BackupPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (!Directory.EnumerateFileSystemEntries(folder).Any())
                {
                    Directory.Delete(folder);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsSameOrInside(string path, string folder) =>
        string.Equals(path, folder, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
