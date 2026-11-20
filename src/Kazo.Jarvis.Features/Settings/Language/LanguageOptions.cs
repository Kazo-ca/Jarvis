using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Settings.Language;

/// <summary>Setting of the assistant language (ADR 0008, section 1), section <c>Settings.Language</c> of <c>config.json</c>.</summary>
public sealed record LanguageOptions
{
    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<LanguageOptions> Section { get; } = new(
        "Settings.Language",
        () => new LanguageOptions(),
        value => value.Code is null || AssistantLanguage.TryParse(value.Code, out _)
            ? []
            : [$"'{value.Code}' is not a supported language: {string.Join(", ", AssistantLanguage.Supported)}."]);

    /// <summary>
    /// The chosen language, <c>fr</c> or <c>en</c>. <see langword="null"/> until the user chooses: the language
    /// proposed from the Windows display language is used.
    /// </summary>
    public string? Code { get; init; }
}
