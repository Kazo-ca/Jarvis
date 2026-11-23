using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Agents.Catalog;

/// <summary>
/// What Jarvis remembers of the agent files it wrote itself, section <c>Agents.Files</c> of <c>config.json</c>: the
/// fingerprint of each starting file as written. A file whose text still has that fingerprint was not touched by
/// the user and may follow a new version of Jarvis; any other file is the user's and is never overwritten
/// (ADR 0025, section 2).
/// </summary>
public sealed record AgentsOptions
{
    /// <summary>The section.</summary>
    public static SettingsSection<AgentsOptions> Section { get; } = new("Agents.Files", () => new AgentsOptions(), Validate);

    /// <summary>The SHA-256 of each starting file as Jarvis last wrote it, by agent identity.</summary>
    public IReadOnlyDictionary<string, string> Written { get; init; } = new Dictionary<string, string>();

    private static IReadOnlyList<string> Validate(AgentsOptions value) =>
        value.Written is null ? ["The list of the written agent files is missing."] : [];
}
