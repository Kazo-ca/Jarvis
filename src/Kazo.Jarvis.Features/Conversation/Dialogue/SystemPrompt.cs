using System.Globalization;
using System.Resources;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>
/// The system message (ADR 0007, section 6): the personality of the language (US1.15), changed by the user or the
/// butler of the resources, then fixed technical rules: the language of the answer, a text made to be said, the date
/// and time of the moment.
/// </summary>
public sealed class SystemPrompt
{
    /// <summary>
    /// The resources of the prompt. Read in a given culture, not in the language of the assistant like the
    /// <c>IStringLocalizer</c> of the pages: the personality page shows both languages at once.
    /// </summary>
    private static readonly ResourceManager Texts = new(typeof(PromptTexts));

    private readonly ISettingsStore settings;
    private readonly TimeProvider time;

    /// <summary>Creates the builder.</summary>
    /// <param name="settings">The settings, for <see cref="ConversationOptions.Section"/>.</param>
    /// <param name="time">The clock of the date written in the rules.</param>
    public SystemPrompt(ISettingsStore settings, TimeProvider time)
    {
        this.settings = settings;
        this.time = time;
    }

    /// <summary>The butler of the resources for <paramref name="language"/>.</summary>
    /// <param name="language">The language.</param>
    /// <returns>The default personality.</returns>
    public static string DefaultPersonality(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return Texts.GetString("Personality", language.Culture) ?? string.Empty;
    }

    /// <summary>The personality used now for <paramref name="language"/>: the user's, else the default one.</summary>
    /// <param name="language">The language.</param>
    /// <returns>The personality.</returns>
    public string Personality(AssistantLanguage language) =>
        settings.Read(ConversationOptions.Section).CustomPersonality(language) ?? DefaultPersonality(language);

    /// <summary>The whole system message for a turn in <paramref name="language"/>.</summary>
    /// <param name="language">The language of the turn.</param>
    /// <returns>The personality, a blank line, then the fixed rules.</returns>
    public string Build(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        var rules = Texts.GetString("Rules", language.Culture) ?? "{0}";

        // The local time, written in the language of the answer: "jeudi 20 novembre 2026 05:15:00".
        var now = time.GetLocalNow();
        var moment = now.ToString("F", language.Culture);
        return Personality(language).Trim() + "\n\n" + string.Format(CultureInfo.InvariantCulture, rules, moment);
    }
}

/// <summary>Marker of the resources <c>PromptTexts.resx</c> and <c>PromptTexts.fr.resx</c>.</summary>
public sealed class PromptTexts
{
    private PromptTexts()
    {
    }
}
