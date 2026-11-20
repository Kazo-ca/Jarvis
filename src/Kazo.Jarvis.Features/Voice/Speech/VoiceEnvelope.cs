using Kazo.Jarvis.Kernel.Audio;

namespace Kazo.Jarvis.Features.Voice.Speech;

/// <summary>
/// The level of a clip over time, computed before it plays, so the orb follows the voice (ADR 0005, section 5)
/// without reading the sound card.
/// </summary>
public static class VoiceEnvelope
{
    /// <summary>Levels per second, the most the sound level feed passes on.</summary>
    public const int StepsPerSecond = SoundLevelFeed.MaximumRatePerSecond;

    /// <summary>Quietest level shown, in decibels; the same scale as the microphone.</summary>
    private const double FloorDecibels = -60;

    /// <summary>One level from 0 to 1 per <c>1/30</c> s of <paramref name="clip"/>.</summary>
    /// <param name="clip">The clip about to play.</param>
    /// <returns>The levels, empty for an empty clip.</returns>
    public static float[] Compute(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (clip.SampleRate <= 0 || clip.Samples.IsEmpty)
        {
            return [];
        }

        var samples = clip.Samples.Span;
        var window = Math.Max(1, clip.SampleRate / StepsPerSecond);
        var levels = new float[(samples.Length + window - 1) / window];
        for (var step = 0; step < levels.Length; step++)
        {
            var part = samples.Slice(step * window, Math.Min(window, samples.Length - (step * window)));
            double sum = 0;
            foreach (var sample in part)
            {
                sum += sample * sample;
            }

            var rms = Math.Sqrt(sum / part.Length);
            var decibels = rms > 0 ? 20 * Math.Log10(rms) : FloorDecibels;
            levels[step] = (float)Math.Clamp((decibels - FloorDecibels) / -FloorDecibels, 0, 1);
        }

        return levels;
    }
}
