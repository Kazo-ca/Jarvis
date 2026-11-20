using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Speech;

namespace Kazo.Jarvis.Features.Voice.Speech;

/// <summary>
/// The voice of each language (US1.10, US1.11), section <c>Voice.Speech</c> of <c>config.json</c>. Only this
/// slice reads it; the voices page goes through <see cref="IVoiceSelection"/>.
/// </summary>
public sealed record SpeechOptions
{
    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<SpeechOptions> Section { get; } = new("Voice.Speech", () => new SpeechOptions(), Validate);

    /// <summary>
    /// The chosen voice by language code (<c>fr</c>, <c>en</c>). A missing language takes its default
    /// (<see cref="SpeechDefaults"/>).
    /// </summary>
    public IReadOnlyDictionary<string, VoiceChoice> Voices { get; init; } = new Dictionary<string, VoiceChoice>();

    /// <summary>The choice of <paramref name="language"/>, or its default.</summary>
    /// <param name="language">The language.</param>
    /// <returns>The voice and speed.</returns>
    public VoiceChoice ChoiceFor(AssistantLanguage language) =>
        Voices.TryGetValue(language.Code, out var choice) && choice is not null ? choice : SpeechDefaults.For(language);

    /// <summary>A copy where <paramref name="language"/> speaks with <paramref name="choice"/>.</summary>
    /// <param name="language">The language.</param>
    /// <param name="choice">The voice and speed.</param>
    /// <returns>The new options.</returns>
    public SpeechOptions With(AssistantLanguage language, VoiceChoice choice) =>
        this with { Voices = new Dictionary<string, VoiceChoice>(Voices) { [language.Code] = choice } };

    private static IReadOnlyList<string> Validate(SpeechOptions options)
    {
        var problems = new List<string>();
        foreach (var (code, choice) in options.Voices ?? new Dictionary<string, VoiceChoice>())
        {
            if (!AssistantLanguage.TryParse(code, out _))
            {
                problems.Add($"Unknown language '{code}'.");
            }

            if (choice is null || string.IsNullOrWhiteSpace(choice.Engine) || string.IsNullOrWhiteSpace(choice.Voice))
            {
                problems.Add($"The voice of '{code}' names no engine or no voice.");
            }
            else if (!double.IsFinite(choice.Speed) || choice.Speed < VoiceChoice.MinimumSpeed || choice.Speed > VoiceChoice.MaximumSpeed)
            {
                problems.Add($"The speed of '{code}' is outside {VoiceChoice.MinimumSpeed} to {VoiceChoice.MaximumSpeed}.");
            }
        }

        return problems;
    }
}
