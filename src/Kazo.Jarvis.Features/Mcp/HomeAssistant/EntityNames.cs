using System.Globalization;
using System.Text;

namespace Kazo.Jarvis.Features.Mcp.HomeAssistant;

/// <summary>
/// Finds the device a spoken name means among the exposed ones. Home Assistant only takes a name or an alias as it
/// is written; the model writes what it heard: « lumière articulée » for the light named « Articulé » (acceptance
/// of stage 2, asked by the maintainer). The match is by words, never by resemblance of letters: a name is taken
/// only when one device, and only one, carries exactly the words said.
/// </summary>
public static class EntityNames
{
    /// <summary>Words that carry no name: articles and prepositions, French and English.</summary>
    private static readonly HashSet<string> Empty = new(StringComparer.Ordinal)
    {
        "le", "la", "les", "l", "un", "une", "des", "du", "de", "d", "au", "aux", "a", "the", "of", "in", "my", "mon", "ma", "mes",
    };

    /// <summary>Words that say the kind of device, by domain: said with the name, seldom part of it.</summary>
    private static readonly Dictionary<string, HashSet<string>> Kinds = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["light"] = ["lumière", "lampe", "light", "éclairage", "luminaire"],
        ["switch"] = ["interrupteur", "prise", "switch", "plug", "outlet"],
        ["fan"] = ["ventilateur", "fan"],
        ["cover"] = ["store", "rideau", "volet", "blind", "shade", "curtain", "cover"],
        ["media_player"] = ["télévision", "télé", "tv", "speaker", "enceinte"],
        ["vacuum"] = ["aspirateur", "vacuum"],
        ["climate"] = ["thermostat"],
    }.ToDictionary(kind => kind.Key, kind => kind.Value.Select(word => Stem(Plain(word))).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

    /// <summary>
    /// The exposed name the model meant by <paramref name="said"/>, or <see langword="null"/> when no device, or
    /// more than one, fits: the name then stays as the model wrote it.
    /// </summary>
    /// <param name="said">The name the model wrote.</param>
    /// <param name="candidates">The devices the other arguments of the call leave possible.</param>
    /// <returns>The name of the only device that fits, as Home Assistant writes it.</returns>
    public static string? Match(string said, IReadOnlyList<HomeEntity> candidates)
    {
        ArgumentNullException.ThrowIfNull(said);
        ArgumentNullException.ThrowIfNull(candidates);

        // First the words as said; then without the word that says the kind (« lumière »): a device named « Lampe du
        // salon » keeps its word, one named « Articulé » is found without it.
        foreach (var withoutKind in new[] { false, true })
        {
            var found = new List<(HomeEntity Entity, string Name)>();
            foreach (var entity in candidates)
            {
                var wanted = Words(said, withoutKind ? entity.Domain : null);
                if (wanted.Count == 0)
                {
                    continue;
                }

                if (entity.Names.FirstOrDefault(name => Words(name, withoutKind ? entity.Domain : null).SetEquals(wanted)) is { } name)
                {
                    found.Add((entity, name));
                }
            }

            if (found.Count == 1)
            {
                return found[0].Name;
            }

            if (found.Count > 1)
            {
                // Two devices carry these words: choosing is not for the code.
                return null;
            }
        }

        return null;
    }

    /// <summary>The words of <paramref name="text"/> that carry a name: lower case, no accent, no ending of gender or number.</summary>
    internal static HashSet<string> Words(string text, string? domain)
    {
        var kinds = domain is not null && Kinds.TryGetValue(domain, out var known) ? known : null;
        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in Plain(text).Split([' ', '\'', '’', '-', '_', '.', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            if (Empty.Contains(raw))
            {
                continue;
            }

            var word = Stem(raw);
            if (word.Length > 0 && kinds?.Contains(word) != true)
            {
                words.Add(word);
            }
        }

        return words;
    }

    /// <summary>« articulée », « articulés » and « articulé » are one word; a short word or a number stays whole.</summary>
    private static string Stem(string word)
    {
        var end = word.Length;
        while (end > 3 && word[end - 1] is 'e' or 's' && !char.IsDigit(word[end - 2]))
        {
            end--;
        }

        return word[..end];
    }

    private static string Plain(string text)
    {
        var plain = new StringBuilder(text.Length);
        foreach (var letter in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letter) != UnicodeCategory.NonSpacingMark)
            {
                plain.Append(char.ToLowerInvariant(letter));
            }
        }

        return plain.ToString();
    }
}
