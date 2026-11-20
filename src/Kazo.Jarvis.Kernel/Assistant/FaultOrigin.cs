namespace Kazo.Jarvis.Kernel.Assistant;

/// <summary>
/// Which part of Jarvis found a fault (stage1/15-faults). The same <see cref="FaultCause"/> can come from several
/// parts (a missing Whisper, Piper or Ollama model is <see cref="FaultCause.ModelMissing"/> each time): the origin
/// lets the faults slice name the right corrective action and the right setting, and lets a slice clear only
/// its own fault.
/// </summary>
public enum FaultOrigin
{
    /// <summary>The microphone (audio devices slice).</summary>
    Microphone,

    /// <summary>The settings file, read at start.</summary>
    Settings,

    /// <summary>The wake word models.</summary>
    WakeWord,

    /// <summary>The end of speech and Whisper (transcription slice).</summary>
    Transcription,

    /// <summary>The language model and its provider.</summary>
    LanguageModel,

    /// <summary>The voice that says the answer (speech slice).</summary>
    Voice,
}
