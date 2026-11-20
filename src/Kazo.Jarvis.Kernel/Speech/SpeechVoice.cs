namespace Kazo.Jarvis.Kernel.Speech;

/// <summary>
/// One voice an engine can speak with (US1.10, ADR 0006, section 5). Listed by <see cref="ISpeechEngine"/>;
/// the user picks one per language on the voices page (US1.11).
/// </summary>
/// <param name="Engine">The engine that speaks it, for instance <c>piper</c> (<see cref="ISpeechEngine.Engine"/>).</param>
/// <param name="Id">Identifier of the voice inside its engine, for instance <c>fr_FR-tom-medium</c>.</param>
/// <param name="Name">Name shown to the user.</param>
/// <param name="Language">
/// Two-letter code of the language it speaks (<c>fr</c>, <c>en</c>), or <see langword="null"/> for a
/// multilingual voice (the cloud voices), which speaks every language.
/// </param>
/// <param name="LeavesMachine">
/// Whether the text is sent off the PC to be spoken (ADR 0009, section 9). The voices page says so.
/// </param>
public sealed record SpeechVoice(string Engine, string Id, string Name, string? Language, bool LeavesMachine)
{
    /// <summary>The choice that names this voice, with <paramref name="speed"/>.</summary>
    /// <param name="speed">Speaking speed, 1 for the normal one.</param>
    /// <returns>The choice to save.</returns>
    public VoiceChoice ToChoice(double speed = VoiceChoice.NormalSpeed) => new(Engine, Id, speed);

    /// <summary>Whether this voice can speak <paramref name="languageCode"/>.</summary>
    /// <param name="languageCode">Two-letter code, <c>fr</c> or <c>en</c>.</param>
    /// <returns><see langword="true"/> for a voice of that language or a multilingual voice.</returns>
    public bool Speaks(string languageCode) =>
        Language is null || string.Equals(Language, languageCode, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this voice is the one <paramref name="choice"/> names (the speed aside).</summary>
    /// <param name="choice">A saved choice.</param>
    /// <returns><see langword="true"/> when engine and identifier match.</returns>
    public bool Is(VoiceChoice? choice) =>
        choice is not null && string.Equals(Engine, choice.Engine, StringComparison.Ordinal) && string.Equals(Id, choice.Voice, StringComparison.Ordinal);
}

/// <summary>A voice chosen for a language, and its speed: what the settings keep (US1.11).</summary>
/// <param name="Engine">The engine (<see cref="SpeechVoice.Engine"/>).</param>
/// <param name="Voice">The voice inside the engine (<see cref="SpeechVoice.Id"/>).</param>
/// <param name="Speed">Speaking speed, from <see cref="MinimumSpeed"/> to <see cref="MaximumSpeed"/>; 1 is normal.</param>
public sealed record VoiceChoice(string Engine, string Voice, double Speed = VoiceChoice.NormalSpeed)
{
    /// <summary>The normal speed.</summary>
    public const double NormalSpeed = 1.0;

    /// <summary>Slowest speed offered.</summary>
    public const double MinimumSpeed = 0.5;

    /// <summary>Fastest speed offered.</summary>
    public const double MaximumSpeed = 2.0;

    /// <summary>The speed brought inside the offered range.</summary>
    public double ClampedSpeed => double.IsFinite(Speed) ? Math.Clamp(Speed, MinimumSpeed, MaximumSpeed) : NormalSpeed;
}
