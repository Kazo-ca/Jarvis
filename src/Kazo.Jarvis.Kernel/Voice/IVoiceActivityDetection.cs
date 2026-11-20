namespace Kazo.Jarvis.Kernel.Voice;

/// <summary>
/// Voice activity detection for the end of an utterance (ADR 0006, section 3): Silero by sherpa-onnx in the
/// Windows host, simulated in the Web host. The transcription slice keeps an energy threshold as its fallback.
/// </summary>
public interface IVoiceActivityDetection
{
    /// <summary>Creates a detector with a fresh state, for one listening.</summary>
    /// <returns>The detector; dispose it at the end of the listening.</returns>
    /// <exception cref="SpeechRecognitionException">The model file is missing or cannot be loaded.</exception>
    IVoiceActivityDetector Create();
}

/// <summary>Says, frame by frame, whether someone speaks. Used by one thread at a time.</summary>
public interface IVoiceActivityDetector : IDisposable
{
    /// <summary>Samples per frame, 16 kHz mono (512 for Silero, that is 32 ms).</summary>
    int FrameLength { get; }

    /// <summary>Reads the next frame.</summary>
    /// <param name="frame">Exactly <see cref="FrameLength"/> samples, following the previous frame.</param>
    /// <returns><see langword="true"/> when the frame holds speech.</returns>
    bool IsSpeech(ReadOnlySpan<float> frame);
}
