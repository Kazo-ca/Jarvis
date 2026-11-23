using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.ClaudeCode.Launch;

/// <summary>
/// Settings of the Claude Code sessions Jarvis starts (US2.9; ADR 0021, sections 1 and 2), section
/// <c>ClaudeCode.Launch</c> of <c>config.json</c>.
/// </summary>
public sealed record ClaudeLaunchOptions
{
    /// <summary>The cap of a session before the user chose (ADR 0021, section 1).</summary>
    public const decimal DefaultBudgetUsd = 2m;

    /// <summary>The smallest and the largest cap of a session, in US dollars.</summary>
    public const decimal LeastBudgetUsd = 0.1m, MostBudgetUsd = 100m;

    /// <summary>The sessions at the same time before the user chose, and the most that can be chosen.</summary>
    public const int DefaultSessions = 3, MostSessionsAllowed = 10;

    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<ClaudeLaunchOptions> Section { get; } = new("ClaudeCode.Launch", () => new ClaudeLaunchOptions(), Validate);

    /// <summary>The most a session may cost, in US dollars: given to Claude Code, which stops at it.</summary>
    public decimal SessionBudgetUsd { get; init; } = DefaultBudgetUsd;

    /// <summary>The most sessions started by Jarvis at the same time.</summary>
    public int MostSessions { get; init; } = DefaultSessions;

    /// <summary>
    /// The allowed folders marked « de confiance » (ADR 0021, section 2), as the allowed folders resolve them. A
    /// session on such a folder loads the settings of the project, <b>hooks included, run without a question</b>.
    /// Empty by default.
    /// </summary>
    public IReadOnlyList<string> TrustedFolders { get; init; } = [];

    /// <summary>The model given to Claude Code, or <see langword="null"/> for the one it chooses.</summary>
    public string? Model { get; init; }

    /// <summary>Whether <paramref name="folder"/> is marked trusted.</summary>
    /// <param name="folder">A resolved allowed folder.</param>
    /// <returns><see langword="true"/> when it is in <see cref="TrustedFolders"/>.</returns>
    public bool IsTrusted(string folder) =>
        TrustedFolders is not null && TrustedFolders.Contains(folder, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Validate(ClaudeLaunchOptions value)
    {
        var problems = new List<string>();
        if (value.SessionBudgetUsd is < LeastBudgetUsd or > MostBudgetUsd)
        {
            problems.Add($"The cap of a session goes from {LeastBudgetUsd} to {MostBudgetUsd} US dollars.");
        }

        if (value.MostSessions is < 1 or > MostSessionsAllowed)
        {
            problems.Add($"Between 1 and {MostSessionsAllowed} sessions can run at the same time.");
        }

        if (value.TrustedFolders is null || value.TrustedFolders.Any(string.IsNullOrWhiteSpace))
        {
            problems.Add("A trusted folder has no path.");
        }

        if (value.Model is { } model && (model.Length is 0 or > 80 || model.Any(char.IsWhiteSpace)))
        {
            problems.Add("The model is one word of at most 80 characters.");
        }

        return problems;
    }
}
