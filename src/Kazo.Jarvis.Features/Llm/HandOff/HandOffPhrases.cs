using System.Text.RegularExpressions;
using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Localization;

namespace Kazo.Jarvis.Features.Llm.HandOff;

/// <summary>
/// The formulas that force the hand-off (US2.6, ADR 0020, section 3): a closed list per language, compared with
/// the first words of the request by code, never by a model. The name tolerates what Whisper writes for it
/// (« Claude », « Claud », « Clode »). « Claude Code » aims at the target of the tasks.
/// </summary>
public static partial class HandOffPhrases
{
    /// <summary>The formulas of <paramref name="language"/>, as the page shows them.</summary>
    /// <param name="language">The language.</param>
    /// <returns>The formulas for the questions, then for the tasks.</returns>
    public static IReadOnlyList<string> For(AssistantLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return language == AssistantLanguage.French
            ? ["Demande à Claude…", "Demande à Claude Code…"]
            : ["Ask Claude…", "Ask Claude Code…"];
    }

    /// <summary>
    /// Whether <paramref name="request"/> starts with a formula. The formulas of every language are tried: the
    /// language Whisper detected for a short sentence is not sure.
    /// </summary>
    /// <param name="request">The request, as heard or typed.</param>
    /// <returns>The kind and what follows the formula; <see langword="null"/> without a formula or when nothing follows it.</returns>
    public static ForcedHandOff? Match(string? request)
    {
        if (string.IsNullOrWhiteSpace(request))
        {
            return null;
        }

        var match = French().Match(request);
        match = match.Success ? match : English().Match(request);
        if (!match.Success || match.Groups["rest"].Value.Trim() is not { Length: > 0 } rest)
        {
            return null;
        }

        return new ForcedHandOff(match.Groups["code"].Success ? HandOffKind.Task : HandOffKind.Question, rest);
    }

    // « Jarvis, » may be heard again after the wake word. « demande a » : Whisper sometimes drops the accent.
    [GeneratedRegex(@"^\W*(?:jarvis\W+)?demande[zr]?\s+[àa]\s+(?:claude|claud|clode)(?<code>[\s-]+code)?(?![\p{L}\p{Nd}])[\s,.:;!?…-]*(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex French();

    [GeneratedRegex(@"^\W*(?:jarvis\W+)?ask\s+(?:claude|claud|clode)(?<code>[\s-]+code)?(?![\p{L}\p{Nd}])[\s,.:;!?…-]*(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex English();
}
