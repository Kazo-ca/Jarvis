namespace Kazo.Jarvis.Kernel.ClaudeCode;

/// <summary>
/// The Claude Code sessions running on this computer (US2.11, US2.14; ADR 0021, section 6), read from the registry
/// each Claude Code process keeps, Claude Desktop included (spikes E2 and E3). Read-only: nothing is ever written
/// there, and the files of that folder that hold secrets are never opened.
/// </summary>
public interface IClaudeSessionRegistry
{
    /// <summary>Reads the registry now.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The sessions whose process is alive, and whether a registry was found.</returns>
    ValueTask<ClaudeSessionListing> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>What a reading of the registry gave.</summary>
/// <param name="Found">Whether the registry exists: <see langword="false"/> when Claude Code never ran for this user.</param>
/// <param name="Sessions">The sessions whose process is alive.</param>
public sealed record ClaudeSessionListing(bool Found, IReadOnlyList<ClaudeSession> Sessions)
{
    /// <summary>No registry.</summary>
    public static ClaudeSessionListing None { get; } = new(false, []);
}

/// <summary>One Claude Code process, as its registry file tells it. The format is internal to Claude Code: every field but the first three may be missing.</summary>
/// <param name="SessionId">The identifier of the session.</param>
/// <param name="ProcessId">The process.</param>
/// <param name="Folder">The working folder.</param>
/// <param name="Name">The name shown: the title given in Claude Desktop, else the name of the folder.</param>
/// <param name="State">Busy, idle or waiting.</param>
/// <param name="WaitingFor">Why it waits, when <paramref name="State"/> is <see cref="ClaudeSessionState.Waiting"/>.</param>
/// <param name="UpdatedAt">The last activity, when the registry says.</param>
/// <param name="Entrypoint">How it was started: <c>cli</c>, <c>sdk-cli</c>, <c>claude-desktop</c>…</param>
/// <param name="Remote">The address of the Git remote of the folder, when it has one.</param>
/// <param name="Branch">The checked-out branch of the folder, when it has one.</param>
public sealed record ClaudeSession(
    string SessionId,
    int ProcessId,
    string Folder,
    string Name,
    ClaudeSessionState State,
    string? WaitingFor,
    DateTimeOffset? UpdatedAt,
    string? Entrypoint,
    string? Remote = null,
    string? Branch = null)
{
    /// <summary>The entry point of the sessions of code of Claude Desktop (spike E3).</summary>
    public const string DesktopEntrypoint = "claude-desktop";

    /// <summary>Whether Claude Desktop started the session (US2.14).</summary>
    public bool FromDesktop => string.Equals(Entrypoint, DesktopEntrypoint, StringComparison.OrdinalIgnoreCase);
}

/// <summary>The state of a session in the registry.</summary>
public enum ClaudeSessionState
{
    /// <summary>A value this version does not know: the format may have changed.</summary>
    Unknown,

    /// <summary><c>busy</c>: it works.</summary>
    Busy,

    /// <summary><c>idle</c>: its turn ended.</summary>
    Idle,

    /// <summary><c>waiting</c>: it needs the user (a permission, an answer).</summary>
    Waiting,
}
