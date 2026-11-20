using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Kernel.Speech;

/// <summary>
/// References of the cloud voice keys in the vault (US1.10, ADR 0004, section 4). In the kernel because the
/// voices page writes them and the adapters read them.
/// </summary>
public static class SpeechKeys
{
    /// <summary>The ElevenLabs key.</summary>
    public static SecretRef ElevenLabs { get; } = new("voice.elevenlabs.apikey");

    /// <summary>The OpenAI key of the voices, separate from the one of a language model provider.</summary>
    public static SecretRef OpenAi { get; } = new("voice.openai.apikey");
}
