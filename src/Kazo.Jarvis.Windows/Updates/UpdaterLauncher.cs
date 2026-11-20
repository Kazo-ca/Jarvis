using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using Kazo.Jarvis.Kernel.Updates;
using Kazo.Jarvis.Windows.Desktop;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Windows.Updates;

/// <summary>
/// <see cref="IUpdateLauncher"/> of the Windows host (US1.18). Starts <c>Kazo.Jarvis.Updater.exe</c> from the
/// prepared folder of the new version, so the updater never replaces the files it runs from, then quits.
/// The updater waits for this process to end, replaces the files of the installation folder and starts the
/// new version. A development build (run from <c>bin\Debug</c> or <c>bin\Release</c>) never installs.
/// </summary>
internal sealed partial class UpdaterLauncher : IUpdateLauncher
{
    /// <summary>The updater, at the root of a prepared folder.</summary>
    public const string UpdaterExecutable = "Kazo.Jarvis.Updater.exe";

    private readonly QuitSignal quit;
    private readonly ILogger<UpdaterLauncher> logger;

    /// <summary>Creates the launcher.</summary>
    public UpdaterLauncher(QuitSignal quit, ILogger<UpdaterLauncher> logger)
    {
        this.quit = quit;
        this.logger = logger;
        CurrentVersion = typeof(UpdaterLauncher).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(UpdaterLauncher).Assembly.GetName().Version?.ToString()
            ?? "0.0.0";
    }

    /// <summary>The installation folder: the folder of the running application.</summary>
    public static string InstallFolder => Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

    /// <inheritdoc />
    public string CurrentVersion { get; }

    /// <inheritdoc />
    public bool CanInstall => !IsDevelopmentBuild(InstallFolder + Path.DirectorySeparatorChar);

    /// <inheritdoc />
    public bool Launch(string preparedFolder)
    {
        if (!CanInstall)
        {
            return false;
        }

        var updater = Path.Combine(preparedFolder, UpdaterExecutable);
        if (!File.Exists(updater))
        {
            LogMissing(updater);
            return false;
        }

        try
        {
            var start = new ProcessStartInfo(updater) { UseShellExecute = false, WorkingDirectory = preparedFolder };
            start.ArgumentList.Add("--install-dir");
            start.ArgumentList.Add(InstallFolder);
            start.ArgumentList.Add("--wait-pid");
            start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var process = Process.Start(start);
            if (process is null)
            {
                return false;
            }

            LogStarted(updater, process.Id);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            LogStartFailed(updater, exception);
            return false;
        }

        quit.Request();
        return true;
    }

    /// <summary>Whether <paramref name="folder"/> is the output of a build rather than an installation.</summary>
    internal static bool IsDevelopmentBuild(string folder) =>
        folder.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase)
        || folder.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(Level = LogLevel.Error, Message = "The updater {Updater} is missing from the prepared version.")]
    private partial void LogMissing(string updater);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updater {Updater} started (process {ProcessId}); Kazo.Jarvis quits so it can replace the files.")]
    private partial void LogStarted(string updater, int processId);

    [LoggerMessage(Level = LogLevel.Error, Message = "The updater {Updater} could not be started.")]
    private partial void LogStartFailed(string updater, Exception exception);
}
