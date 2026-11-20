using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Speech;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// Simulated local voices (ADR 0003, section 3): named like the default Piper voices so the default choice
/// works without a model, plus one more French voice to choose. The "sound" is a soft hum whose length follows
/// the text, so the orb and the playback behave as with a real voice.
/// </summary>
public sealed class SimulatedSpeechEngine : ISpeechEngine
{
    /// <summary>The voices listed.</summary>
    public static readonly IReadOnlyList<SpeechVoice> Voices =
    [
        new("piper", "fr_FR-tom-medium", "Simulated Tom (fr_FR, medium)", "fr", LeavesMachine: false),
        new("piper", "fr_FR-siwis-medium", "Simulated Siwis (fr_FR, medium)", "fr", LeavesMachine: false),
        new("piper", "en_GB-alan-medium", "Simulated Alan (en_GB, medium)", "en", LeavesMachine: false),
    ];

    private readonly Lock gate = new();
    private readonly List<string> spoken = [];

    /// <summary>Raised after each synthesis.</summary>
    public event EventHandler? Spoke;

    /// <summary>When set, the next syntheses fail with this cause.</summary>
    public SpeechFailure? FailWith { get; set; }

    /// <summary>The voice and text of every synthesis, oldest first (<c>engine:voice@speed text</c>).</summary>
    public IReadOnlyList<string> Spoken
    {
        get
        {
            lock (gate)
            {
                return [.. spoken];
            }
        }
    }

    /// <inheritdoc />
    public string Engine => "piper";

    /// <inheritdoc />
    public bool IsCloud => false;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SpeechVoice>> ListVoicesAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Voices);

    /// <inheritdoc />
    public ValueTask<AudioClip> SynthesizeAsync(SpeechVoice voice, string text, double speed, CancellationToken cancellationToken = default) =>
        SimulatedVoice.Speak(voice, text, speed, FailWith, Record);

    private void Record(string line)
    {
        lock (gate)
        {
            spoken.Add(line);
        }

        Spoke?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// A simulated cloud voice: listed only when <see cref="SpeechKeys.OpenAi"/> is in the in-memory vault, like the
/// real OpenAI engine, so the page shows the warning that the text leaves the PC.
/// </summary>
public sealed class SimulatedCloudSpeechEngine : ISpeechEngine
{
    /// <summary>The voice listed when the key is there.</summary>
    public static readonly SpeechVoice Voice = new("openai", "nova", "Simulated Nova", Language: null, LeavesMachine: true);

    private readonly ISecretVault vault;

    /// <summary>Creates the engine.</summary>
    /// <param name="vault">The in-memory vault.</param>
    public SimulatedCloudSpeechEngine(ISecretVault vault)
    {
        this.vault = vault;
    }

    /// <inheritdoc />
    public string Engine => "openai";

    /// <inheritdoc />
    public bool IsCloud => true;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SpeechVoice>> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        await vault.GetAsync(SpeechKeys.OpenAi, cancellationToken) is null ? [] : [Voice];

    /// <inheritdoc />
    public async ValueTask<AudioClip> SynthesizeAsync(SpeechVoice voice, string text, double speed, CancellationToken cancellationToken = default) =>
        await vault.GetAsync(SpeechKeys.OpenAi, cancellationToken) is null
            ? throw new SpeechException(SpeechFailure.KeyMissing, "No simulated cloud key.")
            : await SimulatedVoice.Speak(voice, text, speed, null, _ => { });
}

/// <summary>The sound of the simulated voices.</summary>
internal static class SimulatedVoice
{
    private const int SampleRate = 22_050;

    /// <summary>Speaking time per character at normal speed, bounded so a test never waits long.</summary>
    private static readonly TimeSpan PerCharacter = TimeSpan.FromMilliseconds(25);

    public static ValueTask<AudioClip> Speak(SpeechVoice voice, string text, double speed, SpeechFailure? failure, Action<string> record)
    {
        if (failure is { } cause)
        {
            throw new SpeechException(cause, $"Simulated failure of {voice.Engine}:{voice.Id}.");
        }

        record(FormattableString.Invariant($"{voice.Engine}:{voice.Id}@{speed:0.0} {text}"));
        var seconds = Math.Clamp(PerCharacter.TotalSeconds * text.Length / Math.Max(speed, 0.1), 0.3, 3);
        var samples = new float[(int)(seconds * SampleRate)];
        for (var i = 0; i < samples.Length; i++)
        {
            // A 180 Hz hum whose loudness rises and falls four times a second, like syllables.
            var syllables = 0.5 + (0.5 * Math.Sin(2 * Math.PI * 4 * i / SampleRate));
            samples[i] = (float)(0.3 * syllables * Math.Sin(2 * Math.PI * 180 * i / SampleRate));
        }

        return ValueTask.FromResult(new AudioClip(samples, SampleRate));
    }
}
