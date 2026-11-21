using System.Text.Json;
using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Permissions;

namespace Kazo.Jarvis.Features.ClaudeCode.Launch;

/// <summary>What a request of a session is, for the sentence of the confirmation.</summary>
internal enum SessionRequestKind
{
    /// <summary>A read of a file or a search.</summary>
    Read,

    /// <summary>A write of a file.</summary>
    Write,

    /// <summary>A command.</summary>
    Command,

    /// <summary>Any other tool.</summary>
    Other,
}

/// <summary>A request of a session, classified.</summary>
/// <param name="Class">Its class.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Path">The path it names, as the session wrote it.</param>
/// <param name="Command">The whole command, for <see cref="SessionRequestKind.Command"/>.</param>
internal sealed record SessionVerdict(ActionClass Class, SessionRequestKind Kind, string? Path = null, string? Command = null);

/// <summary>
/// The class of a permission request of a Claude Code session started by Jarvis: the table of ADR 0021,
/// section 4, applied by code. A session works in its folder and nowhere else: a path outside it, and the files of
/// Claude Code that hold secrets, are forbidden whatever the tool.
/// </summary>
internal static class SessionRules
{
    /// <summary>The tool of Claude Code that runs a command.</summary>
    public const string Bash = "Bash";

    private static readonly HashSet<string> Readers = new(StringComparer.Ordinal) { "Read", "Glob", "Grep" };
    private static readonly HashSet<string> Writers = new(StringComparer.Ordinal) { "Write", "Edit", "MultiEdit", "NotebookEdit" };

    // Where the tools of Claude Code put a path.
    private static readonly string[] PathFields = ["file_path", "notebook_path", "path"];

    /// <summary>
    /// Whether <paramref name="tool"/> writes or runs a command: such a tool never runs without a request to
    /// <c>approve</c>, so a session where it does is stopped (ADR 0021, section 1).
    /// </summary>
    /// <param name="tool">A tool of Claude Code.</param>
    /// <returns><see langword="true"/> for the write tools and <c>Bash</c>.</returns>
    public static bool WritesOrRuns(string tool) => Writers.Contains(tool) || tool == Bash;

    /// <summary>Classifies one request.</summary>
    /// <param name="tool">The tool of Claude Code.</param>
    /// <param name="input">Its input. Data: read, never obeyed.</param>
    /// <param name="folder">The folder of the session.</param>
    /// <param name="allowed">The allowed folders.</param>
    /// <returns>The class and what the request is.</returns>
    public static SessionVerdict Classify(string tool, JsonElement input, string folder, IAllowedFolders allowed)
    {
        var kind = Readers.Contains(tool) ? SessionRequestKind.Read
            : Writers.Contains(tool) ? SessionRequestKind.Write
            : tool == Bash ? SessionRequestKind.Command
            : SessionRequestKind.Other;
        var command = kind == SessionRequestKind.Command && Text(input, "command") is { } text ? text : null;

        // The folder of the session must still be an allowed one: the user may have removed it since the launch.
        if (allowed.Check(folder) is not { Verdict: PathVerdict.Allowed, Resolved: { } root })
        {
            return new SessionVerdict(ActionClass.Forbidden, kind, Command: command);
        }

        string? named = null;
        foreach (var field in PathFields)
        {
            if (Text(input, field) is not { } path)
            {
                continue;
            }

            named ??= path;
            var full = Path.IsPathRooted(path) ? path : Path.Combine(root, path);
            if (allowed.Check(full) is not { Verdict: PathVerdict.Allowed, Resolved: { } resolved } || !IsInside(root, resolved))
            {
                return new SessionVerdict(ActionClass.Forbidden, kind, path, command);
            }
        }

        return kind switch
        {
            SessionRequestKind.Read => new SessionVerdict(ActionClass.Read, kind, named),

            // A write that does not say where cannot be shown to the user: it is not asked.
            SessionRequestKind.Write when named is null => new SessionVerdict(ActionClass.Forbidden, kind),
            _ => new SessionVerdict(ActionClass.Sensitive, kind, named, command),
        };
    }

    private static string? Text(JsonElement input, string field) =>
        input.ValueKind == JsonValueKind.Object && input.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>Whether <paramref name="path"/> is <paramref name="root"/> or under it. Both are resolved paths.</summary>
    private static bool IsInside(string root, string path)
    {
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.Length == root.Length || IsSeparator(path[root.Length]) || IsSeparator(root[^1]);
    }

    private static bool IsSeparator(char character) =>
        character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar;
}
