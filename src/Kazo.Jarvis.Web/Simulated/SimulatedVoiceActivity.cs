using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="IVoiceActivityDetection"/>, in place of Silero: the simulated microphone plays a tone
/// all the time, so speech is whatever the page says it is. <see cref="SayPhrase"/> "speaks" for a moment,
/// then silence ends the utterance.
/// </summary>
public sealed class SimulatedVoiceActivity : IVoiceActivityDetection
{
    /// <summary>How long a phrase lasts.</summary>
    public static readonly TimeSpan PhraseLength = TimeSpan.FromSeconds(1.2);

    private long speakingUntil;

    /// <summary>Whether someone "speaks" now.</summary>
    public bool Speaking => Environment.TickCount64 < Interlocked.Read(ref speakingUntil);

    /// <summary>Speaks for <see cref="PhraseLength"/> from now.</summary>
    public void SayPhrase() => Interlocked.Exchange(ref speakingUntil, Environment.TickCount64 + (long)PhraseLength.TotalMilliseconds);

    /// <inheritdoc />
    public IVoiceActivityDetector Create() => new Detector(this);

    private sealed class Detector(SimulatedVoiceActivity owner) : IVoiceActivityDetector
    {
        public int FrameLength => 512;

        public bool IsSpeech(ReadOnlySpan<float> frame) => owner.Speaking;

        public void Dispose()
        {
        }
    }
}
