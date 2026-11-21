using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Kernel.Tools;

/// <summary>
/// The switches of the tools (ADR 0017, section 6), section <c>Tools.Activation</c> of <c>config.json</c>. It lives
/// in the kernel because the registry, the tools page and the pages of the MCP servers all use it.
/// </summary>
public sealed record ToolActivation
{
    /// <summary>The section.</summary>
    public static SettingsSection<ToolActivation> Section { get; } = new("Tools.Activation", () => new ToolActivation(), Validate);

    /// <summary>The choice of the user, by tool name. A tool absent from it follows <see cref="ITool.EnabledByDefault"/>.</summary>
    public IReadOnlyDictionary<string, bool> Switches { get; init; } = new Dictionary<string, bool>();

    /// <summary>A copy where <paramref name="tool"/> is switched on or off.</summary>
    /// <param name="tool">The name of the tool.</param>
    /// <param name="enabled">The new position of its switch.</param>
    /// <returns>The new value.</returns>
    public ToolActivation With(string tool, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        var switches = new Dictionary<string, bool>(Switches ?? new Dictionary<string, bool>(), StringComparer.Ordinal)
        {
            [tool] = enabled,
        };
        return this with { Switches = switches };
    }

    private static IReadOnlyList<string> Validate(ToolActivation value) =>
        value.Switches is null ? ["The list of tool switches is missing."] : [];
}
