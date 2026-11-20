using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Kazo.Jarvis.Kernel.Localization;

/// <summary>
/// The language of the assistant (ADR 0008, section 1): one setting that fixes the interface texts, the
/// transcription, the voice, the personality and the rule given to the model. Formats (dates, numbers) do
/// not follow it: they keep the Windows regional settings.
/// </summary>
public sealed class AssistantLanguage : IEquatable<AssistantLanguage>
{
    private AssistantLanguage(string code)
    {
        Code = code;
        Culture = CultureInfo.GetCultureInfo(code);
    }

    /// <summary>English, the neutral language of the resources (ADR 0008, section 2).</summary>
    public static AssistantLanguage English { get; } = new("en");

    /// <summary>French.</summary>
    public static AssistantLanguage French { get; } = new("fr");

    /// <summary>
    /// The languages of the first version. Adding one adds resources, a default voice, the Whisper language
    /// and a system message; no slice changes its code (ADR 0008, section 7).
    /// </summary>
    public static IReadOnlyList<AssistantLanguage> Supported { get; } = [English, French];

    /// <summary>Two-letter code, <c>en</c> or <c>fr</c>: the value saved in the settings.</summary>
    public string Code { get; }

    /// <summary>The neutral culture of the language, to look up the texts of the resources.</summary>
    public CultureInfo Culture { get; }

    /// <summary>Finds a supported language by its code, ignoring case.</summary>
    /// <param name="code">The candidate code.</param>
    /// <param name="language">The language when supported.</param>
    /// <returns><see langword="true"/> when <paramref name="code"/> names a supported language.</returns>
    public static bool TryParse([NotNullWhen(true)] string? code, [NotNullWhen(true)] out AssistantLanguage? language)
    {
        language = Supported.FirstOrDefault(candidate => string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase));
        return language is not null;
    }

    /// <summary>
    /// The language proposed for a Windows display language (ADR 0008, section 1): French when Windows is in
    /// French (any region), English otherwise.
    /// </summary>
    /// <param name="culture">The Windows display language.</param>
    /// <returns>The supported language that matches it, or English.</returns>
    public static AssistantLanguage ProposedFor(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return TryParse(culture.TwoLetterISOLanguageName, out var language) ? language : English;
    }

    /// <inheritdoc />
    public bool Equals(AssistantLanguage? other) => other is not null && Code == other.Code;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as AssistantLanguage);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code);

    /// <inheritdoc />
    public override string ToString() => Code;
}
