using Kazo.Jarvis.Kernel.Files;

namespace Kazo.Jarvis.Web.Simulated;

/// <summary>
/// A few folders and files in memory, in place of the disk (ADR 0003, section 3), to show the file tools:
/// <c>/home/demo/Documents</c> with two text files and a link that points outside it, and
/// <c>/home/demo/Private</c>, which no test allows.
/// </summary>
public sealed class SimulatedLocalFiles : ILocalFiles
{
    /// <summary>The folder the tests allow.</summary>
    public const string Documents = "/home/demo/Documents";

    /// <summary>A folder outside the allowed one.</summary>
    public const string Private = "/home/demo/Private";

    /// <summary>A link inside <see cref="Documents"/> whose target is <see cref="Private"/>.</summary>
    public const string LinkToPrivate = Documents + "/lien-prive";

    /// <summary>The folder of a project that has a memory file (stage2/19-projects).</summary>
    public const string KazoOs = "/home/demo/Projets/kazo-os";

    private static readonly Dictionary<string, string> Texts = new(StringComparer.OrdinalIgnoreCase)
    {
        [Documents + "/notes.txt"] = "Liste de courses : farine, œufs, lait.\nAppeler le garage mardi.",
        [Documents + "/recettes/crepes.txt"] = "Crêpes : 250 g de farine, 4 œufs, 500 ml de lait. Laisser reposer une heure.",
        [Documents + "/long.txt"] = string.Concat(Enumerable.Range(1, 400).Select(line => $"Ligne {line} du long fichier.\n")),
        [KazoOs + "/JARVIS.md"] = "# Kazo OS\nReste à faire : le pilote réseau, puis l'écran de démarrage.",
        [Private + "/secret.txt"] = "Ne doit jamais être lu par un outil.",
    };

    private static readonly string[] Folders = ["/", "/home", "/home/demo", Documents, Documents + "/recettes", Private, "/home/demo/Projets", KazoOs];

    /// <inheritdoc />
    public string? Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/'))
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
            }
            else if (part != ".")
            {
                parts.Add(part);
            }
        }

        var full = "/" + string.Join('/', parts);
        return full.StartsWith(LinkToPrivate, StringComparison.OrdinalIgnoreCase)
            && (full.Length == LinkToPrivate.Length || full[LinkToPrivate.Length] == '/')
            ? Private + full[LinkToPrivate.Length..]
            : full;
    }

    /// <inheritdoc />
    public ValueTask<FolderListing> ListAsync(string folder, int most, CancellationToken cancellationToken = default)
    {
        if (Texts.ContainsKey(folder))
        {
            throw new LocalFileException(LocalFileFailure.NotAFolder);
        }

        if (!Folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            throw new LocalFileException(LocalFileFailure.NotFound);
        }

        var prefix = folder.TrimEnd('/') + "/";
        bool IsChild(string path) => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && path.Length > prefix.Length && !path[prefix.Length..].Contains('/');
        var entries = Folders.Where(IsChild).Select(path => new FolderEntry(path[prefix.Length..], true, null, null))
            .Concat(folder.Equals(Documents, StringComparison.OrdinalIgnoreCase) ? [new FolderEntry("lien-prive", true, null, null)] : Array.Empty<FolderEntry>())
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Concat(Texts.Where(file => IsChild(file.Key))
                .Select(file => new FolderEntry(file.Key[prefix.Length..], false, System.Text.Encoding.UTF8.GetByteCount(file.Value), null))
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
        return ValueTask.FromResult(new FolderListing([.. entries.Take(most)], entries.Count));
    }

    /// <inheritdoc />
    public ValueTask<FileExcerpt> ReadTextAsync(string file, int start, int most, CancellationToken cancellationToken = default)
    {
        if (Folders.Contains(file, StringComparer.OrdinalIgnoreCase))
        {
            throw new LocalFileException(LocalFileFailure.NotAFile);
        }

        if (!Texts.TryGetValue(file, out var text))
        {
            throw new LocalFileException(LocalFileFailure.NotFound);
        }

        start = Math.Clamp(start, 0, text.Length);
        return ValueTask.FromResult(new FileExcerpt(text.Substring(start, Math.Min(Math.Max(0, most), text.Length - start)), start, text.Length));
    }
}
