namespace Kazo.Jarvis.Kernel.Files;

/// <summary>
/// The allowed folders (ADR 0009, section 5), as the slice of the file tools keeps them. Whoever else works on
/// files of the PC (a Claude Code session started by Jarvis, ADR 0021) asks here which folders it may work in and
/// whether a path is inside one. Without an implementation, no folder is allowed.
/// </summary>
public interface IAllowedFolders
{
    /// <summary>The allowed folders, resolved; a folder whose path is not usable is left out.</summary>
    IReadOnlyList<string> Roots { get; }

    /// <summary>Checks one path.</summary>
    /// <param name="path">The path a model wrote.</param>
    /// <returns>The verdict, and the resolved path when it is usable.</returns>
    CheckedPath Check(string? path);
}

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
