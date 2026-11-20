using Kazo.Jarvis.Kernel.Audio;

namespace Kazo.Jarvis.Kernel.Speech;

/// <summary>
/// A text-to-speech engine (ADR 0006, section 5): Piper and Kokoro by sherpa-onnx, the cloud voices of
/// ElevenLabs and OpenAI. The Windows host registers the adapters of <c>Kazo.Jarvis.Adapters</c>, the Web host
/// a simulation. Several engines are registered; slices take them all and route by <see cref="Engine"/>.
/// The conversation (through the <c>Voice/Speech</c> slice) and the voices page use this same contract
/// (ADR 0006, section 5).
/// </summary>
public interface ISpeechEngine
{
    /// <summary>Stable name of the engine, kept in the settings: <c>piper</c>, <c>kokoro</c>, <c>elevenlabs</c>, <c>openai</c>.</summary>
    string Engine { get; }

    /// <summary>Whether the text leaves the PC (ADR 0009, section 9).</summary>
    bool IsCloud { get; }

    /// <summary>
    /// The voices usable now: a local voice whose model is in the models folder, a cloud voice when its key is in
    /// the vault (US1.10). Never throws but for cancellation: an engine that cannot list returns nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The voices, possibly none.</returns>
    ValueTask<IReadOnlyList<SpeechVoice>> ListVoicesAsync(CancellationToken cancellationToken = default);

    /// <summary>Speaks <paramref name="text"/> into a clip. Thread-safe.</summary>
    /// <param name="voice">One of the voices this engine listed.</param>
    /// <param name="text">One sentence, or a short text (the voices page).</param>
    /// <param name="speed">Speaking speed, 1 for normal.</param>
    /// <param name="cancellationToken">
    /// Stops the work. A local engine may finish the sentence it is computing (a few hundred milliseconds)
    /// before it notices.
    /// </param>
    /// <returns>The mono clip at the engine's own sample rate.</returns>
    /// <exception cref="SpeechException">The voice cannot be spoken, with the cause.</exception>
    ValueTask<AudioClip> SynthesizeAsync(SpeechVoice voice, string text, double speed, CancellationToken cancellationToken = default);
}
