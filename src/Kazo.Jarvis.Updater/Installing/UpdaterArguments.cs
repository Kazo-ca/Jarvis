using System.Globalization;

namespace Kazo.Jarvis.Updater.Installing;

/// <summary>
/// Command line of <c>Kazo.Jarvis.Updater.exe</c>:
/// <code>
/// Kazo.Jarvis.Updater.exe --install-dir &lt;folder&gt; [--source &lt;folder&gt;] [--wait-pid &lt;id&gt;] [--no-start]
/// </code>
/// </summary>
/// <param name="InstallFolder">The folder of the installed application, whose files are replaced.</param>
/// <param name="SourceFolder">The unpacked new version; by default the updater's own folder.</param>
/// <param name="WaitProcessId">The application process to wait for before touching any file, or <see langword="null"/>.</param>
/// <param name="StartAfterUpdate">Whether to start the application at the end (default).</param>
internal sealed record UpdaterArguments(string InstallFolder, string SourceFolder, int? WaitProcessId, bool StartAfterUpdate)
{
    /// <summary>How to call the updater, shown with an argument error.</summary>
    public const string Usage = "Kazo.Jarvis.Updater.exe --install-dir <folder> [--source <folder>] [--wait-pid <id>] [--no-start]";

    /// <summary>Reads the command line.</summary>
    /// <param name="args">The arguments.</param>
    /// <param name="defaultSource">The source folder when <c>--source</c> is absent: the updater's own folder.</param>
    /// <returns>The arguments.</returns>
    /// <exception cref="ArgumentException">Unknown option, missing value, or no <c>--install-dir</c>.</exception>
    public static UpdaterArguments Parse(IReadOnlyList<string> args, string defaultSource)
    {
        string? install = null;
        var source = defaultSource;
        int? waitFor = null;
        var start = true;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--install-dir":
                    install = Value(args, ref i);
                    break;
                case "--source":
                    source = Value(args, ref i);
                    break;
                case "--wait-pid":
                    var text = Value(args, ref i);
                    waitFor = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
                        ? id
                        : throw new ArgumentException($"'{text}' is not a process id.");
                    break;
                case "--no-start":
                    start = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }

        if (install is null)
        {
            throw new ArgumentException("--install-dir is required.");
        }

        return new UpdaterArguments(Full(install), Full(source), waitFor, start);
    }

    private static string Full(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string Value(IReadOnlyList<string> args, ref int i)
    {
        if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Option '{args[i]}' needs a value.");
        }

        return args[++i];
    }
}
