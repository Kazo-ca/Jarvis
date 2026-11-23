using System.Text.RegularExpressions;

namespace Kazo.Jarvis.Kernel.Tasks;

/// <summary>
/// The project key and the topic key of a task (ADR 0013, section 6). Two clones of the same repository, in two
/// folders, are one project: the list of the sessions groups by project, not by raw path.
/// </summary>
public static partial class TaskKeys
{
    /// <summary>The topic of what is on the main branch, or outside Git.</summary>
    public const string Inbox = "_inbox";

    private static readonly HashSet<string> MainBranches = new(StringComparer.OrdinalIgnoreCase) { "main", "master", "trunk", "HEAD" };

    /// <summary>
    /// The project key: the address of the Git remote, normalized to <c>host/owner/name</c> in lower case, whatever
    /// its form (<c>https://github.com/Kazo-ca/Jarvis.git</c>, <c>git@github.com:Kazo-ca/Jarvis</c>); else the
    /// name of the folder.
    /// </summary>
    /// <param name="remote">The address of the remote, when the folder has one.</param>
    /// <param name="folder">The folder of the work.</param>
    /// <returns>The key; never empty.</returns>
    public static string Project(string? remote, string? folder)
    {
        if (Normalize(remote) is { } key)
        {
            return key;
        }

        var name = FolderName(folder);
        return name.Length == 0 ? Inbox : name;
    }

    /// <summary>The name shown for a project key: its last part (<c>jarvis</c> for <c>github.com/kazo-ca/jarvis</c>).</summary>
    /// <param name="project">A project key.</param>
    /// <returns>The name.</returns>
    public static string ProjectName(string project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var cut = project.LastIndexOf('/');
        return cut < 0 ? project : project[(cut + 1)..];
    }

    /// <summary>
    /// The topic key from a branch: <c>branch:&lt;name&gt;</c>, or <see cref="Inbox"/> for the main branch, a
    /// detached head or no Git. Without a GitHub App the keys <c>pr:&lt;n&gt;</c> and <c>issue:&lt;n&gt;</c> are not
    /// found at stage 2: the branch is enough.
    /// </summary>
    /// <param name="branch">The checked-out branch, when there is one.</param>
    /// <returns>The key.</returns>
    public static string Topic(string? branch)
    {
        var name = branch?.Trim() ?? string.Empty;
        return name.Length == 0 || MainBranches.Contains(name) ? Inbox : "branch:" + name;
    }

    private static string? Normalize(string? remote)
    {
        var text = remote?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        string host;
        string path;
        if (ScpLike().Match(text) is { Success: true } scp)
        {
            // git@github.com:Kazo-ca/Jarvis.git
            (host, path) = (scp.Groups["host"].Value, scp.Groups["path"].Value);
        }
        else if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Host.Length > 0)
        {
            // The user name and the password of an address never enter a key.
            (host, path) = (uri.Host, uri.AbsolutePath);
        }
        else
        {
            return null;
        }

        path = path.Trim('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4];
        }

        return path.Length == 0 ? null : $"{host}/{path}".ToLowerInvariant();
    }

    private static string FolderName(string? folder)
    {
        var trimmed = (folder ?? string.Empty).TrimEnd('/', '\\');
        var cut = trimmed.LastIndexOfAny(['/', '\\']);
        return (cut < 0 ? trimmed : trimmed[(cut + 1)..]).Trim();
    }

    [GeneratedRegex(@"^(?:[^@/\s]+@)?(?<host>[^:/\s]+):(?!/)(?<path>[^\s]+)$")]
    private static partial Regex ScpLike();
}
