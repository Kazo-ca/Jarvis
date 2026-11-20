namespace Kazo.Jarvis.Kernel.Audio;

/// <summary>
/// The sound level stream read by the orb (ADR 0005, section 5): at most 30 values per second per origin.
/// The orb chooses the origin from the look: the microphone while listening, the voice while speaking.
/// </summary>
public interface ISoundLevelSource
{
    /// <summary>Receives every level passed on, on the reporting thread. The handler must return quickly.</summary>
    /// <param name="handler">Called for each level.</param>
    /// <returns>Disposing it ends the subscription.</returns>
    IDisposable Subscribe(Action<SoundLevel> handler);
}
