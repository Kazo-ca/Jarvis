using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Tools.Files;

/// <summary>What the allowed folders say of one path.</summary>
public enum PathVerdict
{
    /// <summary>Inside an allowed folder: it may be read.</summary>
    Allowed,

    /// <summary>Not a usable absolute path.</summary>
    Unusable,

    /// <summary>Outside every allowed folder.</summary>
    Outside,

    /// <summary>A file of Claude Code that is refused everywhere: a session key or an IDE lock.</summary>
    Secret,
}

/// <summary>A path checked against the allowed folders.</summary>
/// <param name="Verdict">Whether it may be read.</param>
/// <param name="Resolved">The full path, links and junctions resolved, when it is usable.</param>
public sealed record CheckedPath(PathVerdict Verdict, string? Resolved);

/// <summary>
/// The allowed folders (ADR 0009, section 5): the file tools see nothing else. A path is compared after it is
/// resolved (full path, links and junctions followed) and without regard to case, so a link inside an allowed folder
/// that points outside is outside.
/// </summary>
public sealed class AllowedFolders
{
    private readonly ISettingsStore settings;
    private readonly ILocalFiles files;

    /// <summary>Creates the check.</summary>
    /// <param name="settings">The settings, for <see cref="FileToolsOptions.Section"/>.</param>
    /// <param name="files">Resolves the paths.</param>
    public AllowedFolders(ISettingsStore settings, ILocalFiles files)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(files);
        this.settings = settings;
        this.files = files;
    }

    /// <summary>The allowed folders, resolved; a folder whose path is not usable is left out.</summary>
    public IReadOnlyList<string> Roots =>
        [.. settings.Read(FileToolsOptions.Section).Folders
            .Select(folder => files.Resolve(folder))
            .OfType<string>()
            .Select(Trim)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The most characters one call returns.</summary>
    public int ResultCharacters => settings.Read(FileToolsOptions.Section).ResultCharacters;

    /// <summary>Checks one path.</summary>
    /// <param name="path">The path a model wrote.</param>
    /// <returns>The verdict, and the resolved path to read when it is allowed.</returns>
    public CheckedPath Check(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || files.Resolve(path.Trim()) is not { } resolved)
        {
            return new CheckedPath(PathVerdict.Unusable, null);
        }

        resolved = Trim(resolved);
        if (!Roots.Any(root => IsInside(root, resolved)))
        {
            return new CheckedPath(PathVerdict.Outside, resolved);
        }

        return new CheckedPath(IsClaudeCodeSecret(resolved) ? PathVerdict.Secret : PathVerdict.Allowed, resolved);
    }

    /// <summary>Whether <paramref name="path"/> is <paramref name="root"/> or under it. Both are resolved paths.</summary>
    internal static bool IsInside(string root, string path)
    {
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // « C:\Notes » does not hold « C:\NotesPrivate »: the next character must be a separator. A root that ends
        // with one (« C:\ ») holds everything under it.
        return path.Length == root.Length || IsSeparator(path[root.Length]) || IsSeparator(root[^1]);
    }

    /// <summary>
    /// The files of Claude Code refused everywhere (ADR 0017, section 3): <c>sessions/*.key</c> and <c>ide/*.lock</c>
    /// under a <c>.claude</c> folder. They open a running session to whoever reads them.
    /// </summary>
    internal static bool IsClaudeCodeSecret(string path)
    {
        var parts = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !parts.Take(parts.Length - 2).Any(part => part.Equals(".claude", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var folder = parts[^2];
        var file = parts[^1];
        return (folder.Equals("sessions", StringComparison.OrdinalIgnoreCase) && file.EndsWith(".key", StringComparison.OrdinalIgnoreCase))
            || (folder.Equals("ide", StringComparison.OrdinalIgnoreCase) && file.EndsWith(".lock", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSeparator(char character) =>
        character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar;

    /// <summary>Without the separator at the end, except for a root (« C:\ », « / ») where it is the path.</summary>
    private static string Trim(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length == 0 || trimmed.EndsWith(':') ? path : trimmed;
    }
}
