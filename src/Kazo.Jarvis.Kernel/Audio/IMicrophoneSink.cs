namespace Kazo.Jarvis.Kernel.Audio;

/// <summary>
/// Where the audio slice writes what the microphone captures. Like the sound level, this stream does not go
/// through the bus (ADR 0002, section 5): it is far too frequent.
/// </summary>
public interface IMicrophoneSink
{
    /// <summary>Appends samples to the listening buffer and passes them on to the subscribers.</summary>
    /// <param name="samples">16 kHz mono samples (<see cref="MicrophoneFormat"/>).</param>
    void Write(ReadOnlySpan<float> samples);

    /// <summary>Forgets everything in the listening buffer (microphone muted or changed).</summary>
    void Clear();
}

/// <summary>
/// What the microphone captures, for the wake word and the transcription (ADR 0006, section 1): a live stream
/// and a ring buffer of the last seconds. The buffer stays in memory and is never written to disk.
/// </summary>
public interface IMicrophoneSource
{
    /// <summary>How many seconds the listening buffer keeps.</summary>
    TimeSpan BufferLength { get; }

    /// <summary>Samples written since the start, a position that only grows (<see cref="IMicrophoneSink.Clear"/> keeps it).</summary>
    long Written { get; }

    /// <summary>Receives the samples as they arrive, on the capture thread. The handler must return quickly.</summary>
    /// <param name="handler">Called for each block of samples.</param>
    /// <returns>Disposing it ends the subscription.</returns>
    IDisposable Subscribe(SamplesHandler handler);

    /// <summary>Copies the most recent samples, oldest first, to the end of <paramref name="destination"/>.</summary>
    /// <param name="destination">Where to copy; its length is the number of samples wanted.</param>
    /// <returns>The number of samples copied, fewer when the buffer holds less.</returns>
    int CopyLatest(Span<float> destination);
}
