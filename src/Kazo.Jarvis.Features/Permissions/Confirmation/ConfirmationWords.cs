using System.Text;
using Kazo.Jarvis.Kernel.Localization;

namespace Kazo.Jarvis.Features.Permissions.Confirmation;

/// <summary>What a spoken answer means.</summary>
public enum SpokenAnswer
{
    /// <summary>The whole answer is one of the agreements of the list.</summary>
    Yes,

    /// <summary>The whole answer is one of the refusals of the list.</summary>
    No,

    /// <summary>Anything else: a refusal (ADR 0009, principle 3).</summary>
    Other,
}

/// <summary>
/// Recognizes a spoken answer (ADR 0023, section 3). Code decides, never a model: the whole transcription, without
/// punctuation or capitals, must be one entry of a closed list of the language of the assistant. "Oui, enfin non"
/// holds "oui" and is not an agreement.
/// </summary>
public static class ConfirmationWords
{
    private static readonly Dictionary<string, (HashSet<string> Yes, HashSet<string> No)> Lists = new(StringComparer.Ordinal)
    {
        ["fr"] = (
            Set("oui", "oui Jarvis", "d'accord", "confirme", "je confirme", "approuve", "vas-y"),
            Set("non", "non Jarvis", "annule", "refuse", "arrête")),
        ["en"] = (
            Set("yes", "yes Jarvis", "confirm", "I confirm", "approve", "go ahead"),
            Set("no", "no Jarvis", "cancel", "deny", "stop")),
    };

    /// <summary>The agreements of <paramref name="language"/>, as written in the ADR.</summary>
    /// <param name="language">The language of the assistant.</param>
    /// <returns>The entries, normalized.</returns>
    public static IReadOnlyCollection<string> Agreements(AssistantLanguage language) => ListsOf(language).Yes;

    /// <summary>Tells what <paramref name="transcription"/> means.</summary>
    /// <param name="transcription">What Whisper heard during the single listening.</param>
    /// <param name="language">The language of the assistant, in which it was transcribed.</param>
    /// <returns>An agreement, a refusal, or anything else.</returns>
    public static SpokenAnswer Interpret(string? transcription, AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        var said = Normalize(transcription);
        var (yes, no) = ListsOf(language);
        return yes.Contains(said) ? SpokenAnswer.Yes
            : no.Contains(said) ? SpokenAnswer.No
            : SpokenAnswer.Other;
    }

    /// <summary>Lower case, no punctuation, one space between words; the apostrophe stays, the hyphen becomes a space.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The words alone.</returns>
    internal static string Normalize(string? text)
    {
        var words = new StringBuilder();
        foreach (var character in (text ?? string.Empty).Normalize(NormalizationForm.FormC).ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                words.Append(character);
            }
            else if (character is '\'' or '’')
            {
                words.Append('\'');
            }
            else if (words.Length > 0 && words[^1] != ' ')
            {
                words.Append(' ');
            }
        }

        return words.ToString().Trim();
    }

    private static (HashSet<string> Yes, HashSet<string> No) ListsOf(AssistantLanguage language) =>
        Lists.TryGetValue(language.Code, out var lists) ? lists : Lists["en"];

    private static HashSet<string> Set(params string[] entries) => new(entries.Select(Normalize), StringComparer.Ordinal);
}
