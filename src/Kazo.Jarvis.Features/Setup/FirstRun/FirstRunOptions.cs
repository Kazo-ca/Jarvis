using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Setup.FirstRun;

/// <summary>Whether the first-run wizard is unfinished (US1.4), section <c>Setup.FirstRun</c> of <c>config.json</c>.</summary>
public sealed record FirstRunOptions
{
    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<FirstRunOptions> Section { get; } = new("Setup.FirstRun", () => new FirstRunOptions());

    /// <summary>
    /// <see langword="true"/> from the moment the wizard opened on a first run until the user finishes it: the language
    /// step already writes <c>config.json</c>, so a wizard closed half-way opens again at the next start. A file written
    /// before work item 14 has no such section and never shows the wizard.
    /// </summary>
    public bool Pending { get; init; }
}
