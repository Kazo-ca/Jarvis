using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Assistant.Orb;

/// <summary>Settings of the orb slice (ADR 0011, section 1), section <c>Assistant.Orb</c> of <c>config.json</c>.</summary>
public sealed record OrbOptions
{
    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<OrbOptions> Section { get; } = new(
        "Assistant.Orb",
        () => new OrbOptions(),
        value => Enum.IsDefined(value.Quality)
            ? []
            : [$"'{value.Quality}' is not an orb quality: {string.Join(", ", Enum.GetNames<OrbQuality>())}."]);

    /// <summary>The version of the orb: high quality by default (ADR 0011, section 1).</summary>
    public OrbQuality Quality { get; init; } = OrbQuality.High;
}
