using Kazo.Jarvis.Kernel.Audio;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated <see cref="IAudioPlayback"/> (ADR 0003, section 3): silent. A clip "plays" for its duration on
/// the selected output, and the page shows what was played.
/// </summary>
public sealed class SimulatedAudioPlayback : IAudioPlayback
{
    /// <summary>The default output.</summary>
    public static readonly AudioDevice Speakers = new("simulated-speakers", "Simulated speakers", IsDefault: true);

    /// <summary>The other output.</summary>
    public static readonly AudioDevice Headphones = new("simulated-headphones", "Simulated headphones", IsDefault: false);

    private readonly Lock gate = new();
    private CancellationTokenSource stopAll = new();
    private string? selected;

    /// <summary>Raised after each clip has been played or stopped.</summary>
    public event EventHandler? Played;

    /// <summary>How many clips have been played.</summary>
    public int PlayedCount { get; private set; }

    /// <summary>The output the last clip went to, or <see langword="null"/> before the first one.</summary>
    public AudioDevice? LastOutput { get; private set; }

    /// <inheritdoc />
    public string? SelectedOutput
    {
        get
        {
            lock (gate)
            {
                return selected;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AudioDevice> ListOutputs() => [Speakers, Headphones];

    /// <inheritdoc />
    public void SelectOutput(string? deviceId)
    {
        lock (gate)
        {
            selected = deviceId;
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayAsync(AudioClip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);
        CancellationToken stop;
        AudioDevice output;
        lock (gate)
        {
            stop = stopAll.Token;
            output = ListOutputs().FirstOrDefault(device => device.Id == selected) ?? Speakers;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stop);
        try
        {
            await Task.Delay(clip.Duration, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopped: the clip ends there, as on Windows.
        }

        lock (gate)
        {
            PlayedCount++;
            LastOutput = output;
        }

        Played?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void StopAll()
    {
        CancellationTokenSource previous;
        lock (gate)
        {
            previous = stopAll;
            stopAll = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }
}
