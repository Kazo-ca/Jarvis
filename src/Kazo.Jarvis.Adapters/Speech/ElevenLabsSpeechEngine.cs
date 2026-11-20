using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kazo.Jarvis.Kernel.Audio;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Speech;

namespace Kazo.Jarvis.Adapters.Speech;

/// <summary>
/// ElevenLabs voices (US1.10, ADR 0006, section 5), listed only when <see cref="SpeechKeys.ElevenLabs"/> is in
/// the vault: the voices of the account, read from the service. The text of each sentence is sent to
/// ElevenLabs (ADR 0009, section 9). The multilingual model speaks French and English.
/// </summary>
public sealed class ElevenLabsSpeechEngine : ISpeechEngine
{
    /// <summary>Name of the engine in the settings.</summary>
    public const string Name = "elevenlabs";

    /// <summary>The fast multilingual model: low delay before the first sound.</summary>
    internal const string Model = "eleven_flash_v2_5";

    private static readonly Uri BaseAddress = new("https://api.elevenlabs.io/v1/");

    /// <summary>The list of voices is read again after this long, or when the key changes.</summary>
    private static readonly TimeSpan ListLifetime = TimeSpan.FromMinutes(10);

    private readonly HttpClient http;
    private readonly ISecretVault? vault;
    private readonly TimeProvider time;
    private readonly SemaphoreSlim listing = new(1, 1);
    private (int KeyHash, DateTimeOffset At, IReadOnlyList<SpeechVoice> Voices)? listed;

    /// <summary>Creates the engine.</summary>
    /// <param name="http">The client; its timeout bounds a call.</param>
    /// <param name="vault">The vault holding the key; without it no voice is listed.</param>
    /// <param name="time">Clock of the list's lifetime.</param>
    public ElevenLabsSpeechEngine(HttpClient http, ISecretVault? vault, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        this.http = http;
        this.vault = vault;
        this.time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Engine => Name;

    /// <inheritdoc />
    public bool IsCloud => true;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SpeechVoice>> ListVoicesAsync(CancellationToken cancellationToken = default)
    {
        if (await CloudSpeech.TryReadKeyAsync(vault, SpeechKeys.ElevenLabs, cancellationToken).ConfigureAwait(false) is not { } key)
        {
            return [];
        }

        await listing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var keyHash = StringComparer.Ordinal.GetHashCode(key);
            if (listed is { } previous && previous.KeyHash == keyHash && time.GetUtcNow() - previous.At < ListLifetime)
            {
                return previous.Voices;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseAddress, "voices"));
            request.Headers.Add("xi-api-key", key);
            IReadOnlyList<SpeechVoice> voices;
            try
            {
                var body = await CloudSpeech.SendAsync(http, request, "ElevenLabs", cancellationToken).ConfigureAwait(false);
                voices = Parse(body);
            }
            catch (Exception exception) when (exception is SpeechException or System.Text.Json.JsonException)
            {
                // The contract: listing never throws. A wrong key or no network lists nothing, until next time.
                return [];
            }

            listed = (keyHash, time.GetUtcNow(), voices);
            return voices;
        }
        finally
        {
            listing.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<AudioClip> SynthesizeAsync(SpeechVoice voice, string text, double speed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var key = await CloudSpeech.TryReadKeyAsync(vault, SpeechKeys.ElevenLabs, cancellationToken).ConfigureAwait(false)
            ?? throw new SpeechException(SpeechFailure.KeyMissing, "No ElevenLabs key in the vault.");
        var address = new Uri(BaseAddress, $"text-to-speech/{Uri.EscapeDataString(voice.Id)}?output_format=pcm_24000");
        using var request = new HttpRequestMessage(HttpMethod.Post, address)
        {
            // ElevenLabs takes a speed from 0.7 to 1.2 only.
            Content = JsonContent.Create(new { text, model_id = Model, voice_settings = new { speed = Math.Clamp(speed, 0.7, 1.2) } }),
        };
        request.Headers.Add("xi-api-key", key);
        var bytes = await CloudSpeech.SendAsync(http, request, "ElevenLabs", cancellationToken).ConfigureAwait(false);
        return new AudioClip(CloudSpeech.FromPcm16(bytes), CloudSpeech.SampleRate);
    }

    /// <summary>The voices of a <c>GET /v1/voices</c> answer.</summary>
    internal static IReadOnlyList<SpeechVoice> Parse(byte[] body)
    {
        var answer = System.Text.Json.JsonSerializer.Deserialize<VoicesAnswer>(body);
        return [.. (answer?.Voices ?? [])
            .Where(voice => !string.IsNullOrWhiteSpace(voice.Id))
            .Select(voice => new SpeechVoice(Name, voice.Id!, $"ElevenLabs {voice.Name ?? voice.Id}", Language: null, LeavesMachine: true))
            .OrderBy(voice => voice.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    private sealed record VoicesAnswer([property: JsonPropertyName("voices")] List<VoiceEntry>? Voices);

    private sealed record VoiceEntry(
        [property: JsonPropertyName("voice_id")] string? Id,
        [property: JsonPropertyName("name")] string? Name);
}
