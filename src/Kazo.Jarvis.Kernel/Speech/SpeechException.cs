namespace Kazo.Jarvis.Kernel.Speech;

/// <summary>Why a voice could not speak (US1.17); the slices name it and its corrective action.</summary>
public enum SpeechFailure
{
    /// <summary>The model files of a local voice are missing or unreadable.</summary>
    ModelMissing,

    /// <summary>A cloud voice has no key in the vault.</summary>
    KeyMissing,

    /// <summary>The cloud service refused the key.</summary>
    KeyRefused,

    /// <summary>The cloud service cannot be reached, or took too long.</summary>
    Unreachable,

    /// <summary>The engine does not know this voice.</summary>
    UnknownVoice,

    /// <summary>Anything else; the detail is in the log.</summary>
    Failed,
}

/// <summary>Thrown by <see cref="ISpeechEngine.SynthesizeAsync"/> with the cause.</summary>
public sealed class SpeechException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="failure">The cause.</param>
    /// <param name="message">Technical detail for the log, in English. Never holds a key nor the spoken text.</param>
    /// <param name="innerException">The original error, if any.</param>
    public SpeechException(SpeechFailure failure, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
    }

    /// <summary>The cause.</summary>
    public SpeechFailure Failure { get; }
}
