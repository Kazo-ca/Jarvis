using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Adapters.ClaudeCode;

/// <summary>
/// Reads the registry of the Claude Code sessions, <c>%USERPROFILE%\.claude\sessions\&lt;pid&gt;.json</c> (spikes E2
/// and E3; ADR 0021, section 6). The format is internal to Claude Code and not documented: a field that is missing
/// is tolerated, a file that cannot be read is skipped. <b>Only the <c>.json</c> files of <c>sessions</c> are
/// opened</b>: <c>sessions/*.key</c> and <c>ide/*.lock</c> hold secrets and are never read. Nothing is written.
/// </summary>
public sealed partial class ClaudeSessionRegistry : IClaudeSessionRegistry
{
    // A registry file is a few hundred bytes; a Git config a few kilobytes. More than this is not one of them.
    private const long MostBytes = 256 * 1024;

    private readonly string sessions;
    private readonly Func<int, bool> alive;
    private readonly Func<string, Stream> open;
    private readonly ILogger<ClaudeSessionRegistry>? logger;

    /// <summary>Creates the reader of the registry of the current user.</summary>
    /// <param name="logger">The log.</param>
    public ClaudeSessionRegistry(ILogger<ClaudeSessionRegistry>? logger = null)
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"), IsAlive, OpenShared, logger)
    {
    }

    /// <summary>Creates a reader of the Claude Code folder <paramref name="claudeHome"/>, for the tests.</summary>
    /// <param name="claudeHome">The folder that holds <c>sessions</c>.</param>
    /// <param name="alive">Whether a process is running.</param>
    /// <param name="open">Opens a file for reading: every file this reader opens goes through it.</param>
    /// <param name="logger">The log.</param>
    internal ClaudeSessionRegistry(string claudeHome, Func<int, bool> alive, Func<string, Stream> open, ILogger<ClaudeSessionRegistry>? logger = null)
    {
        sessions = Path.Combine(claudeHome, "sessions");
        this.alive = alive;
        this.open = open;
        this.logger = logger;
    }

    /// <inheritdoc />
    public ValueTask<ClaudeSessionListing> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sessions))
        {
            return ValueTask.FromResult(ClaudeSessionListing.None);
        }

        var found = new List<ClaudeSession>();
        var git = new Dictionary<string, (string? Remote, string? Branch)>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> files;
        try
        {
            // The pattern is not trusted alone: on Windows « *.json » also matches « .jsonx ».
            files = [.. Directory.EnumerateFiles(sessions, "*.json", SearchOption.TopDirectoryOnly).Where(IsRegistryFile)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (logger is not null)
            {
                LogUnreadable(logger, exception, sessions);
            }
            return ValueTask.FromResult(new ClaudeSessionListing(true, []));
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Read(file) is not { } session || !alive(session.ProcessId))
            {
                // A process that ended without removing its file is not a session any more.
                continue;
            }

            if (!git.TryGetValue(session.Folder, out var repository))
            {
                git[session.Folder] = repository = ReadGit(session.Folder);
            }

            found.Add(session with { Remote = repository.Remote, Branch = repository.Branch });
        }

        return ValueTask.FromResult(new ClaudeSessionListing(true, found));
    }

    /// <summary>Whether <paramref name="file"/> is a registry file: a <c>.json</c> directly in <c>sessions</c>. Never a <c>.key</c>, never a <c>.lock</c>.</summary>
    internal static bool IsRegistryFile(string file) =>
        string.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase)
        && string.Equals(Path.GetFileName(Path.GetDirectoryName(file)), "sessions", StringComparison.OrdinalIgnoreCase);

    private ClaudeSession? Read(string file)
    {
        try
        {
            using var stream = open(file);
            if (stream.Length > MostBytes)
            {
                return null;
            }

            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || Text(root, "sessionId") is not { Length: > 0 } id
                || Text(root, "cwd") is not { Length: > 0 } folder
                || !root.TryGetProperty("pid", out var pid) || pid.ValueKind != JsonValueKind.Number || !pid.TryGetInt32(out var process))
            {
                return null;
            }

            return new ClaudeSession(
                id,
                process,
                folder,
                Text(root, "name") is { Length: > 0 } name ? name : FolderName(folder),
                Text(root, "status")?.ToLowerInvariant() switch
                {
                    "busy" => ClaudeSessionState.Busy,
                    "idle" => ClaudeSessionState.Idle,
                    "waiting" => ClaudeSessionState.Waiting,
                    _ => ClaudeSessionState.Unknown,
                },
                Text(root, "waitingFor"),
                Time(root, "statusUpdatedAt") ?? Time(root, "updatedAt"),
                Text(root, "entrypoint"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Claude Code rewrites these files: a partial read is skipped and read again at the next turn.
            return null;
        }
    }

    /// <summary>The remote <c>origin</c> and the branch of the repository that holds <paramref name="folder"/>, read from its <c>.git</c> without running Git.</summary>
    private (string? Remote, string? Branch) ReadGit(string folder)
    {
        try
        {
            for (var current = new DirectoryInfo(folder); current is not null; current = current.Parent)
            {
                var dot = Path.Combine(current.FullName, ".git");
                var gitFolder = dot;
                if (File.Exists(dot))
                {
                    // A worktree: « gitdir: <path> » points to its own folder, whose « commondir » holds the config.
                    var pointer = ReadSmall(dot)?.Trim();
                    if (pointer is null || !pointer.StartsWith("gitdir:", StringComparison.Ordinal))
                    {
                        return (null, null);
                    }

                    gitFolder = Path.GetFullPath(pointer["gitdir:".Length..].Trim(), current.FullName);
                }
                else if (!Directory.Exists(dot))
                {
                    continue;
                }

                var head = ReadSmall(Path.Combine(gitFolder, "HEAD"))?.Trim();
                var branch = head is not null && head.StartsWith("ref: refs/heads/", StringComparison.Ordinal) ? head["ref: refs/heads/".Length..] : null;
                var common = ReadSmall(Path.Combine(gitFolder, "commondir"))?.Trim();
                var configFolder = common is { Length: > 0 } ? Path.GetFullPath(common, gitFolder) : gitFolder;
                return (Origin(ReadSmall(Path.Combine(configFolder, "config"))), branch);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // A folder that is gone or closed to us: the session is listed under the name of its folder.
        }

        return (null, null);
    }

    /// <summary>The <c>url</c> of the section <c>[remote "origin"]</c> of a Git config.</summary>
    internal static string? Origin(string? config)
    {
        if (config is null)
        {
            return null;
        }

        var inOrigin = false;
        foreach (var raw in config.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inOrigin = line.Replace(" ", string.Empty, StringComparison.Ordinal).Equals("[remote\"origin\"]", StringComparison.OrdinalIgnoreCase);
            }
            else if (inOrigin && line.StartsWith("url", StringComparison.OrdinalIgnoreCase) && line.IndexOf('=', StringComparison.Ordinal) is > 0 and var equals)
            {
                return line[(equals + 1)..].Trim() is { Length: > 0 } url ? url : null;
            }
        }

        return null;
    }

    private string? ReadSmall(string file)
    {
        if (!File.Exists(file))
        {
            return null;
        }

        using var stream = open(file);
        if (stream.Length > MostBytes)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? Time(JsonElement root, string name)
    {
        // Milliseconds since 1970, as JavaScript writes them. A value out of range is a format we do not know.
        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var milliseconds)
            && milliseconds is > 0 and < 253_402_300_800_000)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }

        return null;
    }

    private static string FolderName(string folder)
    {
        var trimmed = folder.TrimEnd('/', '\\');
        var cut = trimmed.LastIndexOfAny(['/', '\\']);
        return cut < 0 ? trimmed : trimmed[(cut + 1)..];
    }

    private static Stream OpenShared(string file) =>
        new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static bool IsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The registry of the Claude Code sessions cannot be listed: {Folder}.")]
    private static partial void LogUnreadable(ILogger logger, Exception exception, string folder);
}
