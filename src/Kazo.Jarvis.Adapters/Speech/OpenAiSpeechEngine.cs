using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Speech;

namespace Kazo.Jarvis.Adapters.Speech;

/// <summary>
/// OpenAI voices (US1.10, ADR 0006, section 5), listed only when <see cref="SpeechKeys.OpenAi"/> is in the
/// vault. The text of each sentence is sent to OpenAI (ADR 0009, section 9). Every voice speaks every language.
/// </summary>
public sealed class OpenAiSpeechEngine : ISpeechEngine
{
    /// <summary>Name of the engine in the settings.</summary>
    public const string Name = "openai";

    /// <summary>The speech model: the small one, the fastest to answer.</summary>
    internal const string Model = "gpt-4o-mini-tts";

    private static readonly Uri Endpoint = new("https://api.openai.com/v1/audio/speech");

    /// <summary>The voices of the speech API; they have no listing call.</summary>
    private static readonly string[] Voices = ["alloy", "ash", "ballad", "coral", "echo", "fable", "nova", "onyx", "sage", "shimmer", "verse"];

    private readonly HttpClient http;
    private readonly ISecretVault? vault;

    /// <summary>Creates the engine.</summary>
    /// <param name="http">The client; its timeout bounds a sentence.</param>
    /// <param name="vault">The vault holding the key; without it no voice is listed.</param>
    public OpenAiSpeechEngine(HttpClient http, ISecretVault? vault)
    {
        ArgumentNullException.ThrowIfNull(http);
        this.http = http;
        this.vault = vault;
    }

    /// <inheritdoc />
    public string Engine => Name;

    /// <inheritdoc />
    public bool IsCloud => true;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SpeechVoice>> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        await CloudSpeech.TryReadKeyAsync(vault, SpeechKeys.OpenAi, cancellationToken).ConfigureAwait(false) is null
            ? []
            : [.. Voices.Select(voice => new SpeechVoice(Name, voice, $"OpenAI {char.ToUpperInvariant(voice[0])}{voice[1..]}", Language: null, LeavesMachine: true))];

    /// <inheritdoc />
    public async ValueTask<AudioClip> SynthesizeAsync(SpeechVoice voice, string text, double speed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var key = await CloudSpeech.TryReadKeyAsync(vault, SpeechKeys.OpenAi, cancellationToken).ConfigureAwait(false)
            ?? throw new SpeechException(SpeechFailure.KeyMissing, "No OpenAI voice key in the vault.");
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            // "pcm": raw 16-bit at 24 kHz, nothing to decode. Speed from 0.25 to 4.
            Content = JsonContent.Create(new { model = Model, input = text, voice = voice.Id, response_format = "pcm", speed = Math.Clamp(speed, 0.25, 4.0) }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var bytes = await CloudSpeech.SendAsync(http, request, "OpenAI", cancellationToken).ConfigureAwait(false);
        return new AudioClip(CloudSpeech.FromPcm16(bytes), CloudSpeech.SampleRate);
    }
}
