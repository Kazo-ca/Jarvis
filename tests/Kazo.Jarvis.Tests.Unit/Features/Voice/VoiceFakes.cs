using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

/// <summary>A recognition whose models can go missing and whose answer is chosen by the test.</summary>
internal sealed class FakeSpeechRecognition : ISpeechRecognition
{
    private readonly Lock gate = new();
    private readonly List<RecognizerSpec> loads = [];

    public bool Missing { get; set; }

    public ComputeDevice? ActualDevice { get; set; }

    public IReadOnlyList<TranscriptSegment> Answer { get; set; } = [new(" Quelle heure est-il ?", 0.01f)];

    public TaskCompletionSource? Gate { get; set; }

    public List<FakeRecognizer> Created { get; } = [];

    public RecognizerSpec[] Loads
    {
        get
        {
            lock (gate)
            {
                return [.. loads];
            }
        }
    }

    public ValueTask<ISpeechRecognizer> LoadAsync(RecognizerSpec spec, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            loads.Add(spec);
        }

        if (Missing)
        {
            throw new SpeechRecognitionException(SpeechRecognitionFailure.ModelMissing, "missing", $"/models/whisper/{spec.Model}.bin");
        }

        var recognizer = new FakeRecognizer(this, spec, ActualDevice ?? spec.Device);
        lock (gate)
        {
            Created.Add(recognizer);
        }

        return ValueTask.FromResult<ISpeechRecognizer>(recognizer);
    }
}

/// <summary>A loaded fake model: remembers what it transcribed.</summary>
internal sealed class FakeRecognizer(FakeSpeechRecognition owner, RecognizerSpec spec, ComputeDevice device) : ISpeechRecognizer
{
    public RecognizerSpec Spec => spec;

    public ComputeDevice Device => device;

    public bool Disposed { get; private set; }

    public List<(int Samples, AssistantLanguage Language)> Transcribed { get; } = [];

    public async Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(ReadOnlyMemory<float> samples, AssistantLanguage language, CancellationToken cancellationToken = default)
    {
        lock (Transcribed)
        {
            Transcribed.Add((samples.Length, language));
        }

        if (owner.Gate is { } wait)
        {
            await wait.Task.WaitAsync(cancellationToken);
        }

        return owner.Answer;
    }

    public void Dispose() => Disposed = true;
}

/// <summary>Silero stand-in: a frame is speech when its samples are loud (above 0.1).</summary>
internal sealed class LoudnessDetection : IVoiceActivityDetection
{
    public bool Missing { get; set; }

    public int Created { get; private set; }

    public IVoiceActivityDetector Create()
    {
        if (Missing)
        {
            throw new SpeechRecognitionException(SpeechRecognitionFailure.ModelMissing, "no silero", "/models/vad/silero_vad.onnx");
        }

        Created++;
        return new Detector();
    }

    private sealed class Detector : IVoiceActivityDetector
    {
        public int FrameLength => 512;

        public bool IsSpeech(ReadOnlySpan<float> frame)
        {
            float sum = 0;
            foreach (var sample in frame)
            {
                sum += Math.Abs(sample);
            }

            return sum / frame.Length > 0.1f;
        }

        public void Dispose()
        {
        }
    }
}
