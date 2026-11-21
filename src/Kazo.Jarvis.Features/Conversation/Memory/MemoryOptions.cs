using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Conversation.Memory;

/// <summary>
/// Settings of the memory (ADR 0019, section 8), section <c>Conversation.Memory</c> of <c>config.json</c>: the
/// general switch, the switch of the automatic memory, and the silence after which an exchange is old.
/// </summary>
public sealed record MemoryOptions
{
    /// <summary>The shortest silence that can be set, in minutes.</summary>
    public const int LeastSilenceMinutes = 5;

    /// <summary>The longest silence that can be set, in minutes: one week.</summary>
    public const int MostSilenceMinutes = 7 * 24 * 60;

    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<MemoryOptions> Section { get; } = new("Conversation.Memory", () => new MemoryOptions(), Validate);

    /// <summary>The general switch. Off: nothing is extracted, summarized or sent; what is kept stays visible.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Whether Jarvis draws facts by itself from what the user says (ADR 0019, answer 1).</summary>
    public bool Automatic { get; init; } = true;

    /// <summary>The silence after which the exchanges before it are old, in minutes (ADR 0019, answer 2: 2 hours).</summary>
    public int SilenceMinutes { get; init; } = 120;

    private static IReadOnlyList<string> Validate(MemoryOptions value) =>
        value.SilenceMinutes is < LeastSilenceMinutes or > MostSilenceMinutes
            ? [$"The silence after which an exchange is old goes from {LeastSilenceMinutes} to {MostSilenceMinutes} minutes."]
            : [];
}
