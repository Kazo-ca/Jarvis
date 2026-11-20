namespace Kazo.Jarvis.Kernel.Voice;

/// <summary>
/// Native contract of the wake word (ADR 0006, section 2): loads the "hey jarvis" models and gives a detector
/// that scores the microphone stream. The real engine runs the three openWakeWord models with ONNX Runtime;
/// the Web host has a simulated one triggered by a button (ADR 0003, section 3).
/// </summary>
public interface IWakeWordEngine
{
    /// <summary>Loads the models. Slow (a few hundred milliseconds): never call it on the UI thread.</summary>
    /// <returns>A new detector, owned by the caller.</returns>
    /// <exception cref="WakeWordModelMissingException">A model file is not in the models folder.</exception>
    IWakeWordDetector Load();
}

/// <summary>
/// Scores the microphone stream, one frame at a time. Keeps the recent frames to score the last ~1.3 s, so one
/// detector serves one stream and is not thread safe.
/// </summary>
public interface IWakeWordDetector : IDisposable
{
    /// <summary>Number of samples of one frame (80 ms at 16 kHz for openWakeWord).</summary>
    int FrameLength { get; }

    /// <summary>Scores the stream after one more frame.</summary>
    /// <param name="frame">Exactly <see cref="FrameLength"/> samples, 16 kHz mono (<see cref="Audio.MicrophoneFormat"/>).</param>
    /// <returns>The score of "hey jarvis" ending now, from 0 to 1.</returns>
    float Process(ReadOnlySpan<float> frame);

    /// <summary>Forgets the frames heard so far, as after a gap in the stream (listening, mute).</summary>
    void Reset();
}
