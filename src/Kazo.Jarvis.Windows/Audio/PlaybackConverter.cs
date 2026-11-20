namespace Kazo.Jarvis.Windows.Audio;

/// <summary>
/// Turns a mono clip into the format of an output: its sample rate (linear interpolation) and its channels
/// (the same sample on each), as interleaved 32-bit float. Plain .NET with no NAudio type: tested on Linux.
/// </summary>
internal static class PlaybackConverter
{
    /// <summary>Converts a whole clip.</summary>
    /// <param name="samples">Mono samples.</param>
    /// <param name="sampleRate">Their sample rate.</param>
    /// <param name="outputRate">Sample rate of the output.</param>
    /// <param name="outputChannels">Channels of the output.</param>
    /// <returns>Interleaved samples for the output.</returns>
    public static float[] Convert(ReadOnlySpan<float> samples, int sampleRate, int outputRate, int outputChannels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(outputRate, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(outputChannels, 1);
        if (samples.IsEmpty)
        {
            return [];
        }

        var frames = (int)((long)samples.Length * outputRate / sampleRate);
        var result = new float[frames * outputChannels];
        var step = (double)sampleRate / outputRate;
        for (var frame = 0; frame < frames; frame++)
        {
            var position = frame * step;
            var index = (int)position;
            var fraction = (float)(position - index);
            var a = samples[Math.Min(index, samples.Length - 1)];
            var b = samples[Math.Min(index + 1, samples.Length - 1)];
            var value = Math.Clamp(a + ((b - a) * fraction), -1f, 1f);
            result.AsSpan(frame * outputChannels, outputChannels).Fill(value);
        }

        return result;
    }
}
