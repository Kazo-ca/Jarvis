using Kazo.Jarvis.Kernel.Localization;

namespace Kazo.Jarvis.Kernel.Voice;

/// <summary>
/// Local speech recognition (ADR 0006, section 4): loads a Whisper model once, then transcribes utterances. The
/// Windows host provides it with Whisper.net, the Web host simulates it.
/// </summary>
public interface ISpeechRecognition
{
    /// <summary>Loads <paramref name="spec"/> and keeps it in memory until the recognizer is disposed.</summary>
    /// <param name="spec">The model and the device.</param>
    /// <param name="cancellationToken">Gives up the wait; a load already in the native library may still finish.</param>
    /// <returns>The loaded recognizer.</returns>
    /// <exception cref="SpeechRecognitionException">The model file is missing or cannot be loaded.</exception>
    ValueTask<ISpeechRecognizer> LoadAsync(RecognizerSpec spec, CancellationToken cancellationToken = default);
}

/// <summary>A loaded model. One transcription at a time; disposing it frees the model.</summary>
public interface ISpeechRecognizer : IDisposable
{
    /// <summary>What was asked for.</summary>
    RecognizerSpec Spec { get; }

    /// <summary>Where it actually runs: the processor when CUDA could not load, even if the graphics card was asked for.</summary>
    ComputeDevice Device { get; }

    /// <summary>Transcribes an utterance in <paramref name="language"/>, without detecting the language (ADR 0008).</summary>
    /// <param name="samples">The utterance, 16 kHz mono (<see cref="Audio.MicrophoneFormat"/>).</param>
    /// <param name="language">The language of the assistant.</param>
    /// <param name="cancellationToken">Stops the transcription.</param>
    /// <returns>The segments Whisper found, in order; none for silence.</returns>
    /// <exception cref="SpeechRecognitionException">The native library failed.</exception>
    Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(ReadOnlyMemory<float> samples, AssistantLanguage language, CancellationToken cancellationToken = default);
}

/// <summary>One segment of a transcription, as Whisper gives it.</summary>
/// <param name="Text">The text, possibly with leading spaces or a marker such as <c>[BLANK_AUDIO]</c>.</param>
/// <param name="NoSpeechProbability">Whisper's estimate that the segment holds no speech, from 0 to 1.</param>
public sealed record TranscriptSegment(string Text, float NoSpeechProbability);
