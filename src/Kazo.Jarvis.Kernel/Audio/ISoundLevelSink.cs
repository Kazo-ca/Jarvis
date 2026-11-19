namespace Kazo.Jarvis.Kernel.Audio;

/// <summary>
/// Where the audio slices report their sound level. This stream does not go through the bus
/// (ADR 0002, section 5): it is too frequent.
/// </summary>
public interface ISoundLevelSink
{
    /// <summary>
    /// Reports a level. It is clamped to 0..1 and passed on at most 30 times per second per origin; the
    /// extra values are dropped, except silence (0), which always passes so the orb never stays lit.
    /// </summary>
    /// <param name="origin">Microphone or voice.</param>
    /// <param name="value">The level, from 0 to 1.</param>
    void Report(SoundOrigin origin, float value);
}
