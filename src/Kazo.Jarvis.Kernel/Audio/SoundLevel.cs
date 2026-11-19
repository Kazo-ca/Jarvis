namespace Kazo.Jarvis.Kernel.Audio;

/// <summary>Where a sound level comes from (ADR 0005, section 5).</summary>
public enum SoundOrigin
{
    /// <summary>The microphone, shown while listening.</summary>
    Microphone,

    /// <summary>The synthesized voice, shown while speaking.</summary>
    Voice,
}

/// <summary>One sound level sample.</summary>
/// <param name="Origin">Microphone or voice.</param>
/// <param name="Value">From 0 (silence) to 1.</param>
public readonly record struct SoundLevel(SoundOrigin Origin, float Value);
