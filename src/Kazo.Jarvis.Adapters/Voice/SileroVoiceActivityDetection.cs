using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Voice;
using SherpaOnnx;

namespace Kazo.Jarvis.Adapters.Voice;

/// <summary>
/// <see cref="IVoiceActivityDetection"/> on Silero, run by sherpa-onnx 1.13.8 (ADR 0006, section 3). Silero only
/// says whether each 32 ms frame holds speech; the transcription slice counts the silence itself.
/// </summary>
public sealed class SileroVoiceActivityDetection : IVoiceActivityDetection
{
    /// <summary>Silero reads 512 samples at a time at 16 kHz.</summary>
    public const int WindowSize = 512;

    private readonly JarvisPaths paths;

    /// <summary>Creates the detection.</summary>
    /// <param name="paths">Where the models folder is.</param>
    public SileroVoiceActivityDetection(JarvisPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        this.paths = paths;
    }

    /// <inheritdoc />
    public IVoiceActivityDetector Create()
    {
        var path = VoiceModelFiles.SileroPath(paths);
        if (!File.Exists(path))
        {
            throw new SpeechRecognitionException(SpeechRecognitionFailure.ModelMissing, "The Silero model is not in the models folder.", path);
        }

        var config = new VadModelConfig
        {
            SileroVad = new SileroVadModelConfig
            {
                Model = path,
                Threshold = 0.5f,

                // The slice decides when the utterance ends: Silero only needs to follow the voice closely.
                MinSilenceDuration = 0.1f,
                MinSpeechDuration = 0.1f,
                WindowSize = WindowSize,
                MaxSpeechDuration = 30f,
            },
            SampleRate = MicrophoneFormat.SampleRate,
            NumThreads = 1,
            Provider = "cpu",
            Debug = 0,
        };

        try
        {
            return new Detector(new VoiceActivityDetector(config, bufferSizeInSeconds: 30f));
        }
        catch (Exception exception)
        {
            throw new SpeechRecognitionException(SpeechRecognitionFailure.LoadFailed, $"sherpa-onnx could not load Silero: {exception.Message}", path, exception);
        }
    }

    private sealed class Detector(VoiceActivityDetector vad) : IVoiceActivityDetector
    {
        private readonly float[] window = new float[WindowSize];

        public int FrameLength => WindowSize;

        public bool IsSpeech(ReadOnlySpan<float> frame)
        {
            frame.CopyTo(window);
            vad.AcceptWaveform(window);
            var speech = vad.IsSpeechDetected();

            // The detector also keeps the speech segments; nobody reads them, so they are dropped.
            while (!vad.IsEmpty())
            {
                vad.Pop();
            }

            return speech;
        }

        public void Dispose() => vad.Dispose();
    }
}
