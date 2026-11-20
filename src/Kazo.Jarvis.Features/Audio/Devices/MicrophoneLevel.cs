namespace Kazo.Jarvis.Features.Audio.Devices;

/// <summary>Turns a block of samples into the sound level the orb shows (ADR 0005, section 5).</summary>
public static class MicrophoneLevel
{
    /// <summary>Quietest level shown, in decibels relative to full scale: below it, the orb shows silence.</summary>
    public const float FloorDecibels = -60f;

    /// <summary>
    /// The level of <paramref name="samples"/>, from 0 to 1: their root mean square in decibels, mapped
    /// linearly from <see cref="FloorDecibels"/> (0) to 0 dB (1). Decibels follow what the ear hears; a raw
    /// mean square would leave a normal voice near zero.
    /// </summary>
    /// <param name="samples">Samples from -1 to 1.</param>
    /// <returns>The level; 0 for an empty block.</returns>
    public static float Of(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
        {
            return 0f;
        }

        double sum = 0;
        foreach (var sample in samples)
        {
            sum += sample * sample;
        }

        var rms = Math.Sqrt(sum / samples.Length);
        if (rms <= 0)
        {
            return 0f;
        }

        var decibels = 20 * Math.Log10(rms);
        return (float)Math.Clamp((decibels - FloorDecibels) / -FloorDecibels, 0, 1);
    }
}
