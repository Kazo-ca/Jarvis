using System.Globalization;
using System.Text;

namespace Kazo.Jarvis.Features.ClaudeCode.Launch;

/// <summary>
/// The folder of a work handed to Claude Code (ADR 0021, section 1): an allowed folder the request names, by
/// code and never by a model. « Kazo Bar » names the folders <c>kazo-bar</c>, <c>KazoBar</c> and <c>Kazo Bar</c>.
/// When the request does not name exactly one, Jarvis asks.
/// </summary>
internal static class FolderChoice
{
    // A folder name said aloud is a few words at most.
    private const int MostWords = 4;

    /// <summary>The name said and shown for a folder: its last part.</summary>
    /// <param name="folder">A path.</param>
    /// <returns>The name; the path itself for a root.</returns>
    public static string NameOf(string folder)
    {
        var trimmed = folder.TrimEnd('/', '\\');
        var cut = trimmed.LastIndexOfAny(['/', '\\']);
        var name = cut < 0 ? trimmed : trimmed[(cut + 1)..];
        return name.Length == 0 ? folder : name;
    }

    /// <summary>The allowed folders <paramref name="request"/> names.</summary>
    /// <param name="request">The request, as heard or typed.</param>
    /// <param name="folders">The allowed folders.</param>
    /// <returns>The folders named, in the order of <paramref name="folders"/>.</returns>
    public static IReadOnlyList<string> Named(string request, IReadOnlyList<string> folders)
    {
        var words = Words(request);
        var said = new HashSet<string>(StringComparer.Ordinal);
        for (var first = 0; first < words.Count; first++)
        {
            var run = new StringBuilder();
            for (var count = 0; count < MostWords && first + count < words.Count; count++)
            {
                said.Add(run.Append(words[first + count]).ToString());
            }
        }

        return [.. folders.Where(folder => string.Concat(Words(NameOf(folder))) is { Length: > 0 } name && said.Contains(name))];
    }

    /// <summary>The words of a text: lower case, without accents, cut at everything that is not a letter or a digit.</summary>
    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                word.Append(char.ToLowerInvariant(character));
            }
            else if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }

        if (word.Length > 0)
        {
            words.Add(word.ToString());
        }

        return words;
    }
}
