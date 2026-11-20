using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="IWakeWordEngine"/> (ADR 0003, section 3): a low score on every frame of the simulated
/// microphone, until a button of the page "says" the wake word; the next frames then rise above the threshold
/// and fall back, like a real "hey jarvis".
/// </summary>
public sealed class SimulatedWakeWordEngine : IWakeWordEngine
{
    /// <summary>The score of a frame without the wake word.</summary>
    public const float Background = 0.02f;

    // About 400 ms of a spoken "hey jarvis": rise, peak, fall.
    private static readonly float[] Spoken = [0.2f, 0.6f, 0.93f, 0.97f, 0.7f, 0.3f];

    private readonly Lock gate = new();
    private readonly Queue<float> pending = new();

    /// <inheritdoc />
    public IWakeWordDetector Load() => new Detector(this);

    /// <summary>The next frames score like a spoken "hey jarvis".</summary>
    public void Say()
    {
        lock (gate)
        {
            foreach (var value in Spoken)
            {
                pending.Enqueue(value);
            }
        }
    }

    private float Next()
    {
        lock (gate)
        {
            return pending.TryDequeue(out var value) ? value : Background;
        }
    }

    private sealed class Detector(SimulatedWakeWordEngine owner) : IWakeWordDetector
    {
        public int FrameLength => 1_280;

        public float Process(ReadOnlySpan<float> frame) => owner.Next();

        public void Reset()
        {
            // Nothing heard is kept; a "hey jarvis" said just before still comes out.
        }

        public void Dispose()
        {
        }
    }
}
