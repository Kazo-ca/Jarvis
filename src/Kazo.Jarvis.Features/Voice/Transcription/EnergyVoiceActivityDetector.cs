using Kazo.Jarvis.Kernel.Voice;

namespace Kazo.Jarvis.Features.Voice.Transcription;

/// <summary>
/// The fallback detector of ADR 0006, section 3: a frame is speech when its energy stands clearly above the
/// background noise. The noise is the quietest level of the last seconds (speech always has short dips between
/// syllables), so a fan or a hum is learned after a few seconds.
/// </summary>
public sealed class EnergyVoiceActivityDetector : IVoiceActivityDetector
{
    /// <summary>30 ms at 16 kHz.</summary>
    public const int DefaultFrameLength = 480;

    /// <summary>Margin above the noise for a frame to count as speech, in decibels.</summary>
    public const float MarginDecibels = 12f;

    /// <summary>Below this level a frame is always silence, in decibels (full scale).</summary>
    public const float AbsoluteFloorDecibels = -50f;

    /// <summary>
    /// The noise assumed before the detector has heard long enough, in decibels: a quiet room. It lets a request
    /// spoken right after the activation be heard, and a louder noise is learned once the window is full.
    /// </summary>
    public const float AssumedNoiseDecibels = -55f;

    /// <summary>Frames over which the noise is the minimum: 100 frames of 30 ms, 3 s.</summary>
    public const int NoiseWindowFrames = 100;

    private const float SilenceDecibels = -100f;

    private readonly float[] levels = new float[NoiseWindowFrames];
    private int next;

    /// <summary>Creates the detector, assuming a quiet room until it has heard enough.</summary>
    public EnergyVoiceActivityDetector()
    {
        Array.Fill(levels, AssumedNoiseDecibels);
    }

    /// <inheritdoc />
    public int FrameLength => DefaultFrameLength;

    /// <summary>The background noise learned so far, in decibels.</summary>
    public float NoiseFloor => levels.Min();

    /// <summary>The level of a frame, in decibels (full scale), from its mean square.</summary>
    /// <param name="frame">The samples.</param>
    /// <returns>The level, -100 for digital silence.</returns>
    public static float LevelOf(ReadOnlySpan<float> frame)
    {
        if (frame.IsEmpty)
        {
            return SilenceDecibels;
        }

        double sum = 0;
        foreach (var sample in frame)
        {
            sum += sample * sample;
        }

        var meanSquare = sum / frame.Length;
        return meanSquare <= 1e-10 ? SilenceDecibels : (float)(10 * Math.Log10(meanSquare));
    }

    /// <inheritdoc />
    public bool IsSpeech(ReadOnlySpan<float> frame)
    {
        var level = LevelOf(frame);
        levels[next] = level;
        next = (next + 1) % levels.Length;
        return level >= AbsoluteFloorDecibels && level >= NoiseFloor + MarginDecibels;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing native to free.
    }
}
