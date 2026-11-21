using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.ClaudeCode.Sessions;

/// <summary>How the Claude Code sessions of the computer are followed, section <c>ClaudeCode.Sessions</c> of <c>config.json</c>.</summary>
public sealed record ClaudeSessionsOptions
{
    /// <summary>The shortest and the longest time between two readings of the registry, in seconds.</summary>
    public const int LeastSeconds = 2;

    /// <inheritdoc cref="LeastSeconds"/>
    public const int MostSeconds = 60;

    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<ClaudeSessionsOptions> Section { get; } = new("ClaudeCode.Sessions", () => new ClaudeSessionsOptions(), Validate);

    /// <summary>
    /// Whether the registry is read. On by default: the reading is local, writes nothing and needs no setting
    /// (spike E2). Off, Jarvis does not look at the Claude Code folder of the user at all.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The time between two readings, in seconds. One reading of 14 sessions took 73 ms (spike E2).</summary>
    public int PollSeconds { get; init; } = 5;

    private static IReadOnlyList<string> Validate(ClaudeSessionsOptions value) =>
        value.PollSeconds is < LeastSeconds or > MostSeconds
            ? [$"The time between two readings goes from {LeastSeconds} to {MostSeconds} seconds."]
            : [];
}
