using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Speech;

namespace Kazo.Jarvis.Features.Voice.Speech;

/// <summary>The default voices (ADR 0006, section 5): Piper, <c>fr_FR-tom-medium</c> and <c>en_GB-alan-medium</c>.</summary>
public static class SpeechDefaults
{
    /// <summary>Name of the Piper engine (<see cref="ISpeechEngine.Engine"/>).</summary>
    public const string PiperEngine = "piper";

    /// <summary>The default French voice.</summary>
    public const string FrenchVoice = "fr_FR-tom-medium";

    /// <summary>The default English voice.</summary>
    public const string EnglishVoice = "en_GB-alan-medium";

    /// <summary>The default choice of <paramref name="language"/>.</summary>
    /// <param name="language">The language.</param>
    /// <returns>A Piper voice at normal speed.</returns>
    public static VoiceChoice For(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return new VoiceChoice(PiperEngine, language == AssistantLanguage.French ? FrenchVoice : EnglishVoice);
    }
}
