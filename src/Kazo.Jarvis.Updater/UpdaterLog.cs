namespace Kazo.Jarvis.Updater;

/// <summary>
/// <c>%USERPROFILE%\.kazo\jarvis\logs\updater.log</c>, next to the application's logs. The updater references
/// no project, so it repeats the path of <c>JarvisPaths.Logs</c> (ADR 0004, section 1).
/// </summary>
internal static class UpdaterLog
{
    /// <summary>The user's data, never touched by an update.</summary>
    public static readonly string UserDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kazo", "jarvis");

    /// <summary>The log file.</summary>
    public static readonly string File = Path.Combine(UserDataFolder, "logs", "updater.log");

    /// <summary>Appends a line; a log that cannot be written never stops the update.</summary>
    /// <param name="message">The line.</param>
    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(File)!);
            System.IO.File.AppendAllText(File, $"{DateTimeOffset.Now:O} [{Environment.ProcessId}] {message}{Environment.NewLine}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
