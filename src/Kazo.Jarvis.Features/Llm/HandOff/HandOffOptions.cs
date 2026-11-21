using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Llm.HandOff;

/// <summary>
/// The targets of the hand-off (US2.7, ADR 0020, section 1), section <c>Llm.HandOff</c> of <c>config.json</c>.
/// Nothing is chosen by default: as long as the target of the questions is empty, nothing leaves for a cloud
/// provider.
/// </summary>
public sealed record HandOffOptions
{
    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<HandOffOptions> Section { get; } = new("Llm.HandOff", () => new HandOffOptions(), Validate);

    /// <summary>
    /// Id of the cloud provider that answers the questions handed off, among the configured ones;
    /// <see langword="null"/> when there is no target.
    /// </summary>
    public string? QuestionsProviderId { get; init; }

    /// <summary>
    /// Whether the kept facts and the summary of the memory go with a question (ADR 0020, section 4). Off by
    /// default: only the request and the recent conversation leave.
    /// </summary>
    public bool WithMemory { get; init; }

    private static IReadOnlyList<string> Validate(HandOffOptions value) =>
        value.QuestionsProviderId is { } id && (id.Length is 0 or > 40 || id.Any(char.IsWhiteSpace))
            ? ["The target of the questions is not the id of a provider."]
            : [];
}
