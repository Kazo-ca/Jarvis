namespace Kazo.Jarvis.Kernel.Audio;

/// <summary>
/// Audio playback (ADR 0003, section 5): list the outputs, play, stop at once (US1.12). The Windows host
/// implements it with WASAPI, the Web host simulates it. Callable from any thread.
/// </summary>
public interface IAudioPlayback
{
    /// <summary>The outputs usable now, the Windows default among them.</summary>
    /// <returns>An empty list when there is none.</returns>
    IReadOnlyList<AudioDevice> ListOutputs();

    /// <summary>The output chosen by the user, or <see langword="null"/> for the Windows default.</summary>
    string? SelectedOutput { get; }

    /// <summary>
    /// Chooses the output of the next clips. A missing output falls back on the Windows default. A clip already
    /// playing finishes where it started.
    /// </summary>
    /// <param name="deviceId">The output, or <see langword="null"/> for the Windows default.</param>
    void SelectOutput(string? deviceId);

    /// <summary>Plays <paramref name="clip"/> on the selected output. Clips played together are mixed by Windows.</summary>
    /// <param name="clip">The sound to play.</param>
    /// <param name="cancellationToken">Stops this clip at once.</param>
    /// <returns>Completes when the clip has been played or stopped.</returns>
    /// <exception cref="AudioDeviceUnavailableException">No output can be opened.</exception>
    ValueTask PlayAsync(AudioClip clip, CancellationToken cancellationToken = default);

    /// <summary>Stops every clip at once (interruption, US1.12). Their <see cref="PlayAsync"/> complete.</summary>
    void StopAll();
}

/// <summary>A mono sound to play.</summary>
/// <param name="Samples">Samples from -1 to 1.</param>
/// <param name="SampleRate">Samples per second (Piper produces 22 050, Kokoro 24 000).</param>
public sealed record AudioClip(ReadOnlyMemory<float> Samples, int SampleRate)
{
    /// <summary>How long the clip lasts.</summary>
    public TimeSpan Duration => SampleRate <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)Samples.Length / SampleRate);
}
