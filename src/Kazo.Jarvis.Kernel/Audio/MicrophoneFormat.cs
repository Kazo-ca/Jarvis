namespace Kazo.Jarvis.Kernel.Audio;

/// <summary>
/// The format every capture delivers (ADR 0006, section 1): 16 kHz mono, samples as <see cref="float"/> from
/// -1 to 1. It is the format of the wake word and of Whisper; the host converts from the device format.
/// </summary>
public static class MicrophoneFormat
{
    /// <summary>Samples per second.</summary>
    public const int SampleRate = 16_000;

    /// <summary>Number of samples for <paramref name="duration"/>.</summary>
    /// <param name="duration">A duration, zero or more.</param>
    /// <returns>The number of samples, rounded down.</returns>
    public static int SamplesFor(TimeSpan duration) => (int)(duration.Ticks * SampleRate / TimeSpan.TicksPerSecond);
}

/// <summary>Receives captured samples, 16 kHz mono (<see cref="MicrophoneFormat"/>). The span is only valid during the call.</summary>
/// <param name="samples">The new samples.</param>
public delegate void SamplesHandler(ReadOnlySpan<float> samples);
