using System.Buffers.Binary;
using System.Net;
using Kazo.Jarvis.Kernel.Secrets;
using Kazo.Jarvis.Kernel.Speech;

namespace Kazo.Jarvis.Adapters.Speech;

/// <summary>What the two cloud engines share: the key, the call, the raw sound they return.</summary>
internal static class CloudSpeech
{
    /// <summary>Sample rate asked of both services: raw 16-bit PCM at 24 kHz, mono.</summary>
    public const int SampleRate = 24_000;

    /// <summary>
    /// The key in the vault, read at each call so a saved key applies at once (ADR 0004, section 4);
    /// <see langword="null"/> when there is none or the vault cannot be read.
    /// </summary>
    public static async Task<string?> TryReadKeyAsync(ISecretVault? vault, SecretRef reference, CancellationToken cancellationToken)
    {
        if (vault is null)
        {
            return null;
        }

        try
        {
            return await vault.GetAsync(reference, cancellationToken).ConfigureAwait(false) is { } value ? value.Reveal() : null;
        }
        catch (SecretVaultException)
        {
            return null;
        }
    }

    /// <summary>Sends <paramref name="request"/>; a failure becomes the matching <see cref="SpeechFailure"/>.</summary>
    public static async Task<byte[]> SendAsync(HttpClient http, HttpRequestMessage request, string service, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new SpeechException(SpeechFailure.Unreachable, $"{service} does not answer: {exception.Message}", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SpeechException(SpeechFailure.Unreachable, $"{service} took too long to answer.", exception);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }

            var failure = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? SpeechFailure.KeyRefused
                : response.StatusCode == HttpStatusCode.NotFound ? SpeechFailure.UnknownVoice : SpeechFailure.Failed;
            throw new SpeechException(failure, $"{service} answered {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
    }

    /// <summary>Little-endian signed 16-bit samples to floats from -1 to 1. A trailing odd byte is dropped.</summary>
    public static float[] FromPcm16(ReadOnlySpan<byte> bytes)
    {
        var samples = new float[bytes.Length / 2];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(i * 2, 2)) / 32768f;
        }

        return samples;
    }
}
