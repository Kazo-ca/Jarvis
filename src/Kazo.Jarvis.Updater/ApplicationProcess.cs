using System.ComponentModel;
using System.Diagnostics;
using Kazo.Jarvis.Updater.Installing;

namespace Kazo.Jarvis.Updater;

/// <summary>Waits for the end of the application and starts it again.</summary>
internal static class ApplicationProcess
{
    /// <summary>The application quits within 10 seconds (ADR 0013, section 4); a margin on top.</summary>
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Waits for process <paramref name="processId"/> to end. If it is still there after 30 seconds and runs
    /// from <paramref name="installFolder"/>, ends it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The application could not be ended.</exception>
    public static void WaitForExit(int processId, string installFolder)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            UpdaterLog.Write($"Process {processId} already ended.");
            return;
        }

        using (process)
        {
            if (process.WaitForExit(ExitTimeout))
            {
                UpdaterLog.Write($"Process {processId} ended.");
                return;
            }

            if (!RunsFrom(process, installFolder))
            {
                throw new InvalidOperationException($"Process {processId} did not end and does not run from {installFolder}; refusing to end it.");
            }

            UpdaterLog.Write($"Process {processId} did not end in {ExitTimeout.TotalSeconds} s; ending it.");
            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(KillTimeout))
            {
                throw new InvalidOperationException($"Kazo.Jarvis (process {processId}) could not be ended.");
            }
        }
    }

    /// <summary>Starts the application of <paramref name="installFolder"/>.</summary>
    public static void Start(string installFolder)
    {
        var exe = Path.Combine(installFolder, PackageInstaller.ApplicationExecutable);
        UpdaterLog.Write($"Starting {exe}.");
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = installFolder })?.Dispose();
    }

    private static bool RunsFrom(Process process, string installFolder)
    {
        try
        {
            var folder = Path.GetDirectoryName(process.MainModule?.FileName);
            return folder is not null && string.Equals(Path.TrimEndingDirectorySeparator(folder), installFolder, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
