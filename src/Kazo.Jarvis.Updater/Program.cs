using System.Globalization;
using System.Runtime.InteropServices;
using Kazo.Jarvis.Updater.Installing;

namespace Kazo.Jarvis.Updater;

/// <summary>
/// Kazo.Jarvis.Updater (US1.18): waits for the application to end, replaces its files with the new version
/// it was started from, then starts it again. No window; a message box only when the update fails.
/// Exit codes: 0 installed, 1 failed (previous version restored), 3 bad arguments, 4 another updater running.
/// </summary>
public static class Program
{
    private const string SingleInstanceName = @"Local\Kazo.Jarvis.Updater";
    private const uint MessageBoxError = 0x10;

    /// <summary>Runs the update.</summary>
    /// <param name="args">See <see cref="UpdaterArguments.Usage"/>.</param>
    /// <returns>The exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        UpdaterArguments arguments;
        try
        {
            arguments = UpdaterArguments.Parse(args, AppContext.BaseDirectory);
        }
        catch (ArgumentException exception)
        {
            UpdaterLog.Write($"Bad arguments: {exception.Message}");
            Show($"{exception.Message}\n\n{UpdaterArguments.Usage}");
            return 3;
        }

        using var single = new Mutex(initiallyOwned: true, SingleInstanceName, out var isFirst);
        if (!isFirst)
        {
            UpdaterLog.Write("Another updater is already running; exiting.");
            return 4;
        }

        UpdaterLog.Write($"Updater started: install={arguments.InstallFolder}, source={arguments.SourceFolder}, wait for={arguments.WaitProcessId?.ToString(CultureInfo.InvariantCulture) ?? "none"}.");
        var installed = false;
        try
        {
            PackageInstaller.EnsureSafe(arguments.InstallFolder, arguments.SourceFolder, UpdaterLog.UserDataFolder);
            if (arguments.WaitProcessId is { } processId)
            {
                ApplicationProcess.WaitForExit(processId, arguments.InstallFolder);
            }

            PackageInstaller.DeleteBackups(arguments.InstallFolder, UpdaterLog.Write);
            new PackageInstaller(UpdaterLog.Write).Apply(arguments.SourceFolder, arguments.InstallFolder, DateTime.UtcNow);
            installed = true;
            UpdaterLog.Write("New version installed.");
        }
        catch (Exception exception)
        {
            UpdaterLog.Write($"Update failed: {exception}");
            Show(Failed(exception.Message));
        }

        if (arguments.StartAfterUpdate)
        {
            try
            {
                // After a failure, the previous version was restored: start it again all the same.
                ApplicationProcess.Start(arguments.InstallFolder);
            }
            catch (Exception exception)
            {
                UpdaterLog.Write($"Could not start the application: {exception}");
            }
        }

        return installed ? 0 : 1;
    }

    private static string Failed(string reason) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr"
            ? $"La mise à jour de Kazo.Jarvis a échoué ; la version précédente est restaurée.\n\n{reason}\n\nLe détail est dans le journal :\n{UpdaterLog.File}"
            : $"The Kazo.Jarvis update failed; the previous version is restored.\n\n{reason}\n\nThe details are in the log:\n{UpdaterLog.File}";

    private static void Show(string text) => MessageBoxW(IntPtr.Zero, text, "Kazo.Jarvis", MessageBoxError);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr window, string text, string caption, uint type);
}
