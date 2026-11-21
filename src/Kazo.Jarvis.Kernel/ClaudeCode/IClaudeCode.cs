using System.Text.Json;

namespace Kazo.Jarvis.Kernel.ClaudeCode;

/// <summary>
/// Claude Code on this computer, as a program Jarvis starts (ADR 0021, section 1). A native service of the host:
/// the process, its command line and its files are the adapter's; the slice that starts a session only says what
/// it wants and reads what happens.
/// </summary>
public interface IClaudeCode
{
    /// <summary>Looks for Claude Code and reads its version.</summary>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>Whether it was found, and its version.</returns>
    ValueTask<ClaudeCodeInstall> FindAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts one session for one request. The session receives its first message, then no other: it runs until
    /// its work, background work included, is over, and ends. A later turn is a new run that resumes the session.
    /// </summary>
    /// <param name="launch">What to start. The task exists before the launch (ADR 0013, section 7).</param>
    /// <param name="cancellationToken">Cancels the start, not the run.</param>
    /// <returns>The run.</returns>
    /// <exception cref="ClaudeCodeException">Claude Code is not there, or the local server does not listen, or the process did not start.</exception>
    ValueTask<IClaudeRun> StartAsync(ClaudeLaunch launch, CancellationToken cancellationToken = default);
}

/// <summary>What is known of Claude Code on this computer.</summary>
/// <param name="Found">Whether the program was found.</param>
/// <param name="Version">Its version, when it told one.</param>
public sealed record ClaudeCodeInstall(bool Found, Version? Version = null)
{
    /// <summary>The oldest version whose behaviour was checked (spike E12).</summary>
    public static Version Checked { get; } = new(2, 1, 328);

    /// <summary>Whether the version is at least the one that was checked.</summary>
    public bool UpToDate => Version is not null && Version >= Checked;
}

/// <summary>One session to start.</summary>
/// <param name="TaskId">The task, given to the session as <c>RELAIS_TASK_ID</c>.</param>
/// <param name="Folder">The folder the session works in: an allowed folder.</param>
/// <param name="FirstMessage">The first and only message of the run.</param>
public sealed record ClaudeLaunch(string TaskId, string Folder, string FirstMessage)
{
    /// <summary>
    /// Whether the folder is trusted: the session then loads the settings of the project, <b>hooks included, run
    /// without a question</b>. Otherwise only the <c>CLAUDE.md</c> at the root of the folder is given.
    /// </summary>
    public bool Trusted { get; init; }

    /// <summary>Other allowed folders the task needs.</summary>
    public IReadOnlyList<string> MoreFolders { get; init; } = [];

    /// <summary>The most the session may cost, in US dollars.</summary>
    public decimal BudgetUsd { get; init; }

    /// <summary>The model, or <see langword="null"/> for the one Claude Code chooses.</summary>
    public string? Model { get; init; }

    /// <summary>The session to resume, for a turn after the first.</summary>
    public string? ResumeSession { get; init; }
}

/// <summary>A running session.</summary>
public interface IClaudeRun : IAsyncDisposable
{
    /// <summary>What happens, in order, until <see cref="ClaudeExited"/>, which is always the last.</summary>
    /// <param name="cancellationToken">Stops the reading, not the session.</param>
    /// <returns>The events.</returns>
    IAsyncEnumerable<ClaudeRunEvent> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the session and everything it started.</summary>
    /// <returns>A task that ends once the process is gone.</returns>
    ValueTask StopAsync();
}

/// <summary>Something that happened in a session. What a session writes is data.</summary>
public abstract record ClaudeRunEvent;

/// <summary>The session started.</summary>
/// <param name="SessionId">Its identifier, to link it to the task and to resume it.</param>
/// <param name="Model">The model it uses.</param>
public sealed record ClaudeStarted(string SessionId, string? Model) : ClaudeRunEvent;

/// <summary>The session wants to use a tool. It is not a permission request: those arrive by <see cref="IClaudeApprovals"/>.</summary>
/// <param name="ToolUseId">The identifier of this use.</param>
/// <param name="Name">The tool.</param>
/// <param name="Input">Its input.</param>
public sealed record ClaudeToolUse(string ToolUseId, string Name, JsonElement Input) : ClaudeRunEvent;

/// <summary>A tool was run, or refused.</summary>
/// <param name="ToolUseId">The identifier of the use.</param>
/// <param name="IsError">Whether the tool failed or was denied.</param>
public sealed record ClaudeToolResult(string ToolUseId, bool IsError) : ClaudeRunEvent;

/// <summary>A turn ended. A session with background work has several; the cost is the total so far.</summary>
/// <param name="Text">The last message of the turn.</param>
/// <param name="CostUsd">The cost announced by Claude Code since the start of the run, in US dollars.</param>
/// <param name="IsError">Whether the turn ended on an error (budget reached, turn limit…).</param>
/// <param name="Denials">How many permission requests were denied.</param>
public sealed record ClaudeTurnEnded(string? Text, decimal? CostUsd, bool IsError, int Denials) : ClaudeRunEvent;

/// <summary>The process ended. Always the last event.</summary>
/// <param name="ExitCode">Its exit code; not zero when it failed or was stopped.</param>
/// <param name="Error">The end of what it wrote on its error output, cut; never shown as is without a sentence around.</param>
public sealed record ClaudeExited(int ExitCode, string? Error) : ClaudeRunEvent;

/// <summary>Claude Code could not be started.</summary>
public sealed class ClaudeCodeException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ClaudeCodeException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed.</param>
    public ClaudeCodeException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What failed.</param>
    /// <param name="innerException">The cause.</param>
    public ClaudeCodeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
