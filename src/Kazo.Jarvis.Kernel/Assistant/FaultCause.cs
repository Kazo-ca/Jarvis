namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>
/// Why the assistant cannot work normally (US1.17). The text, the corrective action and the link to the
/// right setting belong to the slices (work item stage1/15-faults); the kernel only names the cause.
/// </summary>
public enum FaultCause
{
    /// <summary>No usable microphone. Activations are refused while it lasts.</summary>
    MicrophoneMissing,

    /// <summary>The model provider does not answer.</summary>
    ModelUnreachable,

    /// <summary>A model file (wake word, recognition, voice or language model) is missing.</summary>
    ModelMissing,

    /// <summary>No voice can be played.</summary>
    VoiceMissing,

    /// <summary>The configuration file could not be read; the application runs on defaults.</summary>
    ConfigurationUnreadable,
}
