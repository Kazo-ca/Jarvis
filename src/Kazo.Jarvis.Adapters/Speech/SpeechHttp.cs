namespace Kazo.Jarvis.Adapters.Speech;

/// <summary>The HTTP client of the cloud voices, kept apart from those of the models and of the updates.</summary>
/// <param name="Client">The client.</param>
public sealed record SpeechHttp(HttpClient Client);
