using System.Text;
using System.Text.RegularExpressions;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>
/// Cuts the streamed answer into sentences (US1.9): each complete sentence is said while the model writes the next
/// one. A sentence ends at <c>.</c>, <c>!</c>, <c>?</c> or <c>…</c> followed by a space, or at a line break. An
/// abbreviation (« M. », « Dr. »), a number (« 3.5 ») or a lone initial does not end a sentence. Not thread-safe:
/// one splitter per answer.
/// </summary>
public sealed partial class SentenceSplitter
{
    /// <summary>
    /// A sentence longer than this is said up to its last comma or space: on a run-on sentence the voice should not
    /// wait for the whole paragraph.
    /// </summary>
    public const int LongestSentence = 250;

    /// <summary>Words followed by a dot that do not end a sentence, compared without case.</summary>
    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "m", "mm", "mme", "mmes", "mlle", "dr", "pr", "me", "st", "ste", "mr", "mrs", "ms", "prof", "sr", "jr",
        "vs", "env", "av", "apr", "j.-c", "cf", "no", "p", "ex", "e.g", "i.e",
    };

    private readonly StringBuilder pending = new();

    /// <summary>Adds a piece of the answer.</summary>
    /// <param name="text">The text received.</param>
    /// <returns>The sentences that are complete now, in order, ready to be said.</returns>
    public IReadOnlyList<string> Push(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        pending.Append(text);
        var sentences = new List<string>();
        while (TakeSentence() is { } sentence)
        {
            AddSpeakable(sentences, sentence);
        }

        return sentences;
    }

    /// <summary>Ends the answer.</summary>
    /// <returns>What remains, as at most one sentence.</returns>
    public IReadOnlyList<string> Flush()
    {
        var rest = pending.ToString();
        pending.Clear();
        var sentences = new List<string>();
        AddSpeakable(sentences, rest);
        return sentences;
    }

    /// <summary>
    /// <paramref name="text"/> without what is written but not said: Markdown marks (<c>**</c>, <c>#</c>,
    /// back quotes, list bullets). The model is asked not to write them (ADR 0007, section 6), but may.
    /// </summary>
    /// <param name="text">A sentence.</param>
    /// <returns>The sentence to say, trimmed; empty when nothing is left to say.</returns>
    public static string Speakable(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var cleaned = MarkdownMarks().Replace(text, string.Empty);
        cleaned = LineBullet().Replace(cleaned, string.Empty);
        cleaned = Spaces().Replace(cleaned, " ").Trim();
        return cleaned.Any(char.IsLetterOrDigit) ? cleaned : string.Empty;
    }

    private static void AddSpeakable(List<string> sentences, string sentence)
    {
        var speakable = Speakable(sentence);
        if (speakable.Length > 0)
        {
            sentences.Add(speakable);
        }
    }

    /// <summary>Removes and returns the first complete sentence of <see cref="pending"/>, or <see langword="null"/>.</summary>
    private string? TakeSentence()
    {
        var text = pending.ToString();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\n')
            {
                return Take(i + 1);
            }

            if (c is not ('.' or '!' or '?' or '…'))
            {
                continue;
            }

            // Closing marks stay with their sentence: « Bien sûr. » then the space.
            var end = i + 1;
            while (end < text.Length)
            {
                if (text[end] is '.' or '!' or '?' or '…' or '"' or '»' or '”' or ')' or '\'' or '’')
                {
                    end++;
                }
                else if (char.IsWhiteSpace(text[end]) && end + 1 < text.Length && text[end + 1] is '»' or '”' && text[end] != '\n')
                {
                    // French quotes put a space before the closing one: « C'est parfait. »
                    end += 2;
                }
                else
                {
                    break;
                }
            }

            if (end >= text.Length)
            {
                // The next piece decides: "3." may become "3.5", "M." may be followed by a name.
                break;
            }

            if (!char.IsWhiteSpace(text[end]) || (c == '.' && IsAbbreviation(text, i)))
            {
                i = end - 1;
                continue;
            }

            return Take(end);
        }

        if (pending.Length > LongestSentence)
        {
            // A comma is a natural pause; failing one, the last space; failing one, a hard cut.
            var comma = text.LastIndexOf(", ", LongestSentence - 1, StringComparison.Ordinal);
            var cut = comma > 0 ? comma + 1 : text.LastIndexOf(' ', LongestSentence - 1);
            return Take(cut > 0 ? cut : LongestSentence);
        }

        return null;
    }

    private string Take(int length)
    {
        var sentence = pending.ToString(0, length);
        pending.Remove(0, length);
        return sentence;
    }

    /// <summary>Whether the dot at <paramref name="dot"/> closes an abbreviation or an initial rather than a sentence.</summary>
    private static bool IsAbbreviation(string text, int dot)
    {
        var start = dot;
        while (start > 0 && (char.IsLetter(text[start - 1]) || text[start - 1] is '.' or '-'))
        {
            start--;
        }

        var word = text[start..dot];
        if (word.Length == 0)
        {
            // "1. " at the start of a line numbers a list item; the item is the sentence, not its number.
            var digits = dot;
            while (digits > 0 && char.IsDigit(text[digits - 1]))
            {
                digits--;
            }

            // A line break always ends a sentence, so the start of a line is the start of the pending text.
            return digits < dot && string.IsNullOrWhiteSpace(text[..digits]);
        }

        // A lone capital is an initial ("J. R. R. Tolkien"); a lone small letter is a list mark or a unit.
        return Abbreviations.Contains(word) || (word.Length == 1 && char.IsUpper(word[0]));
    }

    [GeneratedRegex(@"\*\*|__|`+|~~|(?<=^|\s)#{1,6}\s|(?<=\s|^)\*(?=\S)|(?<=\S)\*(?=\s|$|[.,!?])")]
    private static partial Regex MarkdownMarks();

    [GeneratedRegex(@"^\s*(?:[-*•]|\d+[.)])\s+")]
    private static partial Regex LineBullet();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
