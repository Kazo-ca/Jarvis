using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Kernel.Speech;

/// <summary>
/// The voice of each language (US1.10, US1.11). Given by the <c>Voice/Speech</c> slice, which owns the
/// setting; read and changed by the voices page (<c>Voice/TestVoice</c>) and, later, by the first run
/// (work item 14), without knowing that slice.
/// </summary>
public interface IVoiceSelection
{
    /// <summary>Raised after a choice was saved.</summary>
    event EventHandler? Changed;

    /// <summary>The saved choice for <paramref name="language"/>, or its default (Piper, ADR 0006, section 5).</summary>
    /// <param name="language">The language.</param>
    /// <returns>The choice; the voice may be missing now.</returns>
    VoiceChoice ChoiceFor(AssistantLanguage language);

    /// <summary>Saves the voice and speed of <paramref name="language"/>.</summary>
    /// <param name="language">The language.</param>
    /// <param name="choice">The voice and speed.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>Whether it was saved.</returns>
    ValueTask<SettingsWriteResult> ChooseAsync(AssistantLanguage language, VoiceChoice choice, CancellationToken cancellationToken = default);

    /// <summary>Every voice usable now, of every engine, local ones first.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The voices.</returns>
    ValueTask<IReadOnlyList<SpeechVoice>> ListVoicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The voice that will speak <paramref name="language"/>: the chosen one when usable, otherwise the default
    /// Piper voice, otherwise any local voice of the language, otherwise <see langword="null"/>. A cloud voice is
    /// never taken unless the user chose it (ADR 0009, section 9).
    /// </summary>
    /// <param name="language">The language.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The voice and the speed, or <see langword="null"/> when no voice can speak it.</returns>
    ValueTask<(SpeechVoice Voice, double Speed)?> ResolveAsync(AssistantLanguage language, CancellationToken cancellationToken = default);
}
