namespace Kazo.Jarvis.Adapters.Files;

/// <summary>
/// Where Kazo.Jarvis keeps its files (ADR 0004, section 1): a single root in the user's folder,
/// <c>%USERPROFILE%\.kazo\jarvis</c>. Nothing goes to <c>%APPDATA%</c> or <c>%LOCALAPPDATA%</c>.
/// Tests give a temporary folder instead.
/// </summary>
/// <param name="Root">The root folder.</param>
public sealed record JarvisPaths(string Root)
{
    /// <summary>The settings, <c>config.json</c>.</summary>
    public string ConfigFile => Path.Combine(Root, "config.json");

    /// <summary>Copies taken before a migration, or of an unreadable file.</summary>
    public string Backups => Path.Combine(Root, "backups");

    /// <summary>Folder of the database.</summary>
    public string Data => Path.Combine(Root, "data");

    /// <summary>The database: conversation, then memory and log.</summary>
    public string DatabaseFile => Path.Combine(Data, "jarvis.db");

    /// <summary>The encrypted secrets file, only when that vault is chosen.</summary>
    public string SecretsFile => Path.Combine(Root, "secrets.json");

    /// <summary>Downloaded models (wake word, recognition, voices).</summary>
    public string Models => Path.Combine(Root, "models");

    /// <summary>Technical logs.</summary>
    public string Logs => Path.Combine(Root, "logs");

    /// <summary>
    /// Downloaded and unpacked new versions, waiting for the updater (US1.18). Created when needed; its
    /// content is deleted at the next start. <c>Kazo.Jarvis.Updater</c> references no project and repeats
    /// this path for its own log.
    /// </summary>
    public string Updates => Path.Combine(Root, "updates");

    /// <summary>
    /// Data of the web views of the Windows host (WebView2 profile), kept out of the installation folder.
    /// Created by WebView2 itself, not by <see cref="EnsureCreated"/>.
    /// </summary>
    public string WebView => Path.Combine(Root, "webview");

    /// <summary>
    /// The data of the browser of Jarvis (ADR 0022, section 2): its cookies and cache, apart from the WebView2 that
    /// shows the interface and from the browser of the user.
    /// </summary>
    public string Browser => Path.Combine(Root, "browser");

    /// <summary>The root of the current Windows user: <c>%USERPROFILE%\.kazo\jarvis</c>.</summary>
    /// <returns>The paths.</returns>
    public static JarvisPaths ForCurrentUser() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kazo", "jarvis"));

    /// <summary>Creates the root and its folders when they are missing. Creates no file.</summary>
    public void EnsureCreated()
    {
        foreach (var folder in new[] { Root, Backups, Data, Models, Logs })
        {
            Directory.CreateDirectory(folder);
        }
    }
}
