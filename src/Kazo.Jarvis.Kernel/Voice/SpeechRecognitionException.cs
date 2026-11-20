namespace Kazo.Jarvis.Kernel.Voice;

/// <summary>Why a model of the voice chain could not be used.</summary>
public enum SpeechRecognitionFailure
{
    /// <summary>The model file is not in the models folder.</summary>
    ModelMissing,

    /// <summary>The file is there but the native library refused it (damaged file, missing runtime).</summary>
    LoadFailed,

    /// <summary>The transcription itself failed.</summary>
    Failed,
}

/// <summary>A model of the voice chain (Whisper, Silero) could not be loaded or used.</summary>
public sealed class SpeechRecognitionException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="failure">The cause.</param>
    /// <param name="message">Technical detail for the log, in English.</param>
    /// <param name="modelPath">The file expected or loaded, shown to the user so the model can be put in place.</param>
    /// <param name="innerException">The original error, if any.</param>
    public SpeechRecognitionException(SpeechRecognitionFailure failure, string message, string? modelPath = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
        ModelPath = modelPath;
    }

    /// <summary>The cause.</summary>
    public SpeechRecognitionFailure Failure { get; }

    /// <summary>The file expected or loaded, when known.</summary>
    public string? ModelPath { get; }
}
