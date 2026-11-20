using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>
/// Settings of the conversation (US1.15, ADR 0008, section 4), section <c>Conversation.Dialogue</c> of
/// <c>config.json</c>: the personality changed by the user, one per language.
/// </summary>
public sealed record ConversationOptions
{
    /// <summary>Longest personality accepted. The 4 096-token context of the local model must keep room for the history.</summary>
    public const int MaximumPersonalityLength = 4000;

    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<ConversationOptions> Section { get; } = new("Conversation.Dialogue", () => new ConversationOptions(), Validate);

    /// <summary>
    /// The personality written by the user, by language code (<c>fr</c>, <c>en</c>). A language absent from it uses
    /// the butler of its resources; « Réinitialiser » removes the language from it.
    /// </summary>
    public IReadOnlyDictionary<string, string> Personalities { get; init; } = new Dictionary<string, string>();

    /// <summary>The personality written by the user for <paramref name="language"/>, or <see langword="null"/>.</summary>
    /// <param name="language">The language.</param>
    /// <returns>The text, or <see langword="null"/> when the default one applies.</returns>
    public string? CustomPersonality(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return Personalities is not null
            && Personalities.TryGetValue(language.Code, out var text)
            && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
    }

    /// <summary>A copy where <paramref name="language"/> uses <paramref name="text"/>, or its default when it is <see langword="null"/>.</summary>
    /// <param name="language">The language.</param>
    /// <param name="text">The new personality, or <see langword="null"/> to reset it.</param>
    /// <returns>The new options.</returns>
    public ConversationOptions WithPersonality(AssistantLanguage language, string? text)
    {
        ArgumentNullException.ThrowIfNull(language);
        var personalities = new Dictionary<string, string>(Personalities ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text))
        {
            personalities.Remove(language.Code);
        }
        else
        {
            personalities[language.Code] = text.Trim();
        }

        return this with { Personalities = personalities };
    }

    private static IReadOnlyList<string> Validate(ConversationOptions value)
    {
        if (value.Personalities is null)
        {
            return ["The list of personalities is missing."];
        }

        var problems = new List<string>();
        foreach (var (code, text) in value.Personalities)
        {
            if (!AssistantLanguage.TryParse(code, out _))
            {
                problems.Add($"'{code}' is not a language of the assistant.");
            }
            else if (text is not null && text.Length > MaximumPersonalityLength)
            {
                problems.Add($"The personality of '{code}' is longer than {MaximumPersonalityLength} characters.");
            }
        }

        return problems;
    }
}
