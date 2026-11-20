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

    // True once the first frame of a spoken "hey jarvis" has been scored and some of it is still to come.
    private bool underway;

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
            if (!pending.TryDequeue(out var value))
            {
                underway = false;
                return Background;
            }

            underway = pending.Count > 0;
            return value;
        }
    }

    // The listener resets its detector when it resumes after a pause. The end of a "hey jarvis" cut by that
    // pause was said during the pause: a real detector never hears it. Without this, its last frames came out
    // seconds later, as the best recent score of the next thing shown.
    private void DropWhatWasCut()
    {
        lock (gate)
        {
            if (underway)
            {
                pending.Clear();
                underway = false;
            }
        }
    }

    private sealed class Detector(SimulatedWakeWordEngine owner) : IWakeWordDetector
    {
        public int FrameLength => 1_280;

        public float Process(ReadOnlySpan<float> frame) => owner.Next();

        // A "hey jarvis" said just before, not started yet, still comes out; one cut by the pause does not.
        public void Reset() => owner.DropWhatWasCut();

        public void Dispose()
        {
        }
    }
}
