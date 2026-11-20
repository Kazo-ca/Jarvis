using Kazo.Jarvis.Kernel.Assistant;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Assistant.Faults;

/// <summary>
/// What the faults window says about a fault (US1.17): the cause in a few words, what happened, what to do, and
/// the settings page that fixes it. Texts are keys of <see cref="FaultWindow"/>'s resources.
/// </summary>
/// <param name="Origin">Where the fault comes from: the one given by the publisher, or the usual one for its cause.</param>
/// <param name="TitleKey">The cause in a few words.</param>
/// <param name="ExplanationKey">What happened and its effect.</param>
/// <param name="ActionKey">The corrective action.</param>
/// <param name="PageKey">The settings page that fixes it, or <see langword="null"/> when none does.</param>
public sealed record FaultAdvice(FaultOrigin Origin, string TitleKey, string ExplanationKey, string ActionKey, string? PageKey)
{
    /// <summary>Key of the audio devices page (slice <c>Audio/Devices</c>).</summary>
    public const string AudioPage = "Audio.Devices";

    /// <summary>Key of the language models page (slice <c>Llm/Providers</c>).</summary>
    public const string LanguageModelsPage = "Llm.Providers";

    /// <summary>Key of the voice models page (slice <c>Setup/Models</c>).</summary>
    public const string VoiceModelsPage = "Setup.Models";

    /// <summary>Key of the voices page (slice <c>Voice/TestVoice</c>).</summary>
    public const string VoicesPage = "Voice.TestVoice";

    /// <summary>Key of the wake word page (slice <c>Voice/WakeWord</c>).</summary>
    public const string WakeWordPage = "Voice.WakeWord";

    /// <summary>The advice for <paramref name="fault"/>.</summary>
    /// <param name="fault">The current fault.</param>
    /// <returns>Its texts and its page.</returns>
    public static FaultAdvice For(AssistantFault fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        var origin = fault.Origin ?? UsualOrigin(fault.Cause);
        return (fault.Cause, origin) switch
        {
            (FaultCause.MicrophoneMissing, _) => Advice(origin, "Microphone", AudioPage),

            // The page of the language models names the cause precisely with its connection test.
            (FaultCause.ModelUnreachable, _) => Advice(origin, "ModelUnreachable", LanguageModelsPage),
            (FaultCause.ModelMissing, FaultOrigin.LanguageModel) => Advice(origin, "LanguageModelMissing", LanguageModelsPage),
            (FaultCause.ModelMissing, FaultOrigin.Voice) => Advice(origin, "VoiceModelMissing", VoiceModelsPage),
            (FaultCause.ModelMissing, FaultOrigin.WakeWord) => Advice(origin, "WakeWordModelMissing", WakeWordPage),
            (FaultCause.ModelMissing, _) => Advice(origin, "WhisperMissing", VoiceModelsPage),
            (FaultCause.VoiceMissing, _) => Advice(origin, "Voice", VoicesPage),

            // No page fixes a file Jarvis refuses to overwrite: the text says which file and what to do.
            (FaultCause.ConfigurationUnreadable, _) => new FaultAdvice(
                origin,
                "Configuration_Title",
                "Configuration_" + ConfigurationReason(fault.Detail),
                "Configuration_Action_" + ConfigurationReason(fault.Detail),
                null),
            _ => Advice(origin, "Unknown", null),
        };
    }

    /// <summary>The origin of a fault whose publisher did not give one.</summary>
    /// <param name="cause">The cause.</param>
    /// <returns>The part of Jarvis that usually finds it.</returns>
    public static FaultOrigin UsualOrigin(FaultCause cause) => cause switch
    {
        FaultCause.MicrophoneMissing => FaultOrigin.Microphone,
        FaultCause.ModelUnreachable => FaultOrigin.LanguageModel,
        FaultCause.ModelMissing => FaultOrigin.Transcription,
        FaultCause.VoiceMissing => FaultOrigin.Voice,
        _ => FaultOrigin.Settings,
    };

    /// <summary>The detail of a <see cref="FaultCause.ConfigurationUnreadable"/> fault: the load outcome.</summary>
    /// <param name="outcome">How the settings file was loaded.</param>
    /// <returns>The detail to publish.</returns>
    public static string ConfigurationDetail(SettingsLoadOutcome outcome) => outcome.ToString();

    private static string ConfigurationReason(string? detail) =>
        Enum.TryParse<SettingsLoadOutcome>(detail, out var outcome) && outcome is SettingsLoadOutcome.NewerVersion or SettingsLoadOutcome.MigrationFailed
            ? outcome.ToString()
            : nameof(SettingsLoadOutcome.Unreadable);

    private static FaultAdvice Advice(FaultOrigin origin, string name, string? page) =>
        new(origin, name + "_Title", name + "_Explanation", name + "_Action", page);
}
