using System.Globalization;
using System.Text;

namespace Kazo.Jarvis.Features.Projects.View;

/// <summary>
/// A subject the user made in a project with « + Nouveau sujet » (ADR 0026, section 11, point 2). It has a thread
/// from the start; the works Jarvis starts from that thread become its tasks.
/// </summary>
public sealed record UserSubject
{
    /// <summary>What the keys of these subjects start with.</summary>
    public const string Prefix = "subject:";

    /// <summary>The identifier of its project, or the one of the built-in project.</summary>
    public string Project { get; init; } = string.Empty;

    /// <summary>Its key in the project: the topic of its tasks.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Its name, as the user wrote it.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>When it was made.</summary>
    public DateTimeOffset MadeAt { get; init; }

    /// <summary>Makes a subject, with a key no subject of the project has.</summary>
    /// <param name="project">The project.</param>
    /// <param name="name">The name the user wrote.</param>
    /// <param name="taken">The keys of the subjects of the project.</param>
    /// <param name="now">The time.</param>
    /// <returns>The subject, or <see langword="null"/> when the name is empty or too long.</returns>
    public static UserSubject? Make(string project, string? name, IEnumerable<string> taken, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentNullException.ThrowIfNull(taken);
        var clean = (name ?? string.Empty).Trim();
        if (clean.Length is 0 or > ProjectsViewOptions.MostNameCharacters)
        {
            return null;
        }

        var used = taken.ToHashSet(StringComparer.Ordinal);
        var slug = Slug(clean);
        var key = Prefix + slug;
        for (var n = 2; used.Contains(key); n++)
        {
            key = Prefix + slug + "-" + n.ToString(CultureInfo.InvariantCulture);
        }

        return new UserSubject { Project = project, Key = key, Name = clean, MadeAt = now };
    }

    // Letters and digits without their accents, in lower case, joined by hyphens.
    private static string Slug(string name)
    {
        var slug = new StringBuilder();
        foreach (var character in name.Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(char.ToLowerInvariant(character));
            }
            else if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var text = slug.ToString().Trim('-');
        return text.Length == 0 ? "sujet" : text;
    }
}
