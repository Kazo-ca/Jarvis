using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Mcp.HomeAssistant;

/// <summary>
/// What the user adds to the classification of Home Assistant (ADR 0018, section 5), section
/// <c>Mcp.HomeAssistant</c> of <c>config.json</c>: the « sécurité » list of entities and scripts, whose calls ask
/// for a click on the computer, and how long the list of the exposed entities is kept in memory.
/// </summary>
public sealed record HomeAssistantOptions
{
    /// <summary>The most entries of each list.</summary>
    public const int MostEntries = 200;

    /// <summary>The longest time the exposed entities are kept, in seconds.</summary>
    public const int MostContextSeconds = 60;

    /// <summary>The section.</summary>
    public static SettingsSection<HomeAssistantOptions> Section { get; } = new("Mcp.HomeAssistant", () => new HomeAssistantOptions(), Validate);

    /// <summary>
    /// The entities the user marked « sécurité », by one of the names Home Assistant gives them. MCP gives no
    /// entity identifier: the name is the only handle.
    /// </summary>
    public IReadOnlyList<string> SecurityEntities { get; init; } = [];

    /// <summary>The scripts the user marked « sécurité », by the name of their tool (<c>script__…</c>).</summary>
    public IReadOnlyList<string> SecurityScripts { get; init; } = [];

    /// <summary>How long the exposed entities read from a server are reused, in seconds. 0 reads them at every call.</summary>
    public int ContextSeconds { get; init; } = 5;

    private static IReadOnlyList<string> Validate(HomeAssistantOptions value)
    {
        var problems = new List<string>();
        foreach (var (list, what) in new[] { (value.SecurityEntities, "entities"), (value.SecurityScripts, "scripts") })
        {
            if (list is null || list.Count > MostEntries || list.Any(string.IsNullOrWhiteSpace))
            {
                problems.Add($"The security list of {what} is missing, has an empty entry or more than {MostEntries} entries.");
            }
        }

        if (value.ContextSeconds is < 0 or > MostContextSeconds)
        {
            problems.Add($"The exposed entities are kept from 0 to {MostContextSeconds} seconds.");
        }

        return problems;
    }
}
