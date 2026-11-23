using System.Globalization;
using System.Resources;
using System.Text;
using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Projects;
using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Projects.Catalog;

/// <summary>What happened to a project the user asked to create.</summary>
public enum ProjectOutcome
{
    /// <summary>Created.</summary>
    Added,

    /// <summary>The name has no letter or digit, or is too long.</summary>
    BadName,

    /// <summary>Another project has this name, whatever the case and the accents.</summary>
    NameTaken,

    /// <summary>The folder is not a usable local path.</summary>
    BadFolder,

    /// <summary><see cref="ProjectsOptions.MostProjects"/> projects exist.</summary>
    Full,

    /// <summary>The settings could not be written.</summary>
    NotSaved,
}

/// <summary>
/// The projects (ADR 0025, sections 6, 10 and 11): a name and a local folder each, kept in the settings, and the
/// memory of a project read from the Markdown file <see cref="IProjectCatalog.MemoryFileName"/> of its folder, so
/// that a Claude Code session working there reads the same file. Nothing is kept between two reads: a file the
/// user or a session changed applies to the next turn.
/// </summary>
public sealed class ProjectCatalog : IProjectCatalog, IDisposable
{
    private static readonly ResourceManager Texts = new(typeof(ProjectTexts));

    private readonly ISettingsStore settings;
    private readonly ILocalFiles? files;
    private readonly IDisposable watch;

    /// <summary>Creates the catalog.</summary>
    /// <param name="settings">The settings, for <see cref="ProjectsOptions.Section"/>.</param>
    /// <param name="files">The files of the PC; without them a project has no memory.</param>
    public ProjectCatalog(ISettingsStore settings, ILocalFiles? files = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
        this.files = files;
        watch = settings.Watch(ProjectsOptions.Section, _ => Changed?.Invoke());
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<ProjectDefinition> Projects =>
        [.. (settings.Read(ProjectsOptions.Section).Projects ?? []).Select(project => new ProjectDefinition(project.Id, project.Name, project.Folder))];

    /// <summary>The characters a project may take in a context of <paramref name="contextLength"/> tokens: one sixteenth, at about 3 characters per token.</summary>
    /// <param name="contextLength">The context of the model, in tokens.</param>
    /// <returns>The budget, in characters.</returns>
    public static int Budget(int contextLength) => Math.Max(0, contextLength) * 3 / 16;

    /// <inheritdoc />
    public ProjectDefinition? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var wanted = Plain(name);
        var known = Projects;
        return known.FirstOrDefault(project => project.Id == name.Trim()) ?? known.FirstOrDefault(project => Plain(project.Name) == wanted);
    }

    /// <inheritdoc />
    public ProjectDefinition? NamedIn(string? request)
    {
        if (string.IsNullOrWhiteSpace(request))
        {
            return null;
        }

        // Whole words only: the project « Os » is not named by « dossier ». The longest name wins: « Kazo Os » before « Kazo ».
        var words = "-" + Plain(request) + "-";
        return Projects
            .OrderByDescending(project => project.Name.Length)
            .FirstOrDefault(project => words.Contains("-" + Plain(project.Name) + "-", StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public async ValueTask<string> RecallAsync(ProjectDefinition project, AssistantLanguage language, int contextLength, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(language);
        var memory = await MemoryAsync(project, Budget(contextLength), cancellationToken).ConfigureAwait(false);
        var block = new StringBuilder();
        block.Append(Text("Block_Rule", language)).Append('\n').Append(Text("Block_Start", language)).Append('\n');
        block.Append(Text("Block_Name", language)).Append(' ').Append(Safe(project.Name)).Append('\n');
        block.Append(Text("Block_Folder", language)).Append(' ').Append(Safe(project.Folder)).Append('\n');
        block.Append(memory is null
            ? Text("Block_NoMemory", language, IProjectCatalog.MemoryFileName)
            : Text("Block_Memory", language, IProjectCatalog.MemoryFileName) + "\n" + memory);
        return block.Append('\n').Append(Text("Block_End", language)).ToString();
    }

    /// <summary>The memory of a project: the start of its file, or <see langword="null"/> when there is none to read.</summary>
    /// <param name="project">The project.</param>
    /// <param name="most">Most characters read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text, without a bracket that could close the block.</returns>
    public async ValueTask<string?> MemoryAsync(ProjectDefinition project, int most, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (files is null || most <= 0 || files.Resolve(MemoryPath(project)) is not { } path)
        {
            return null;
        }

        try
        {
            var excerpt = await files.ReadTextAsync(path, 0, most, cancellationToken).ConfigureAwait(false);
            var text = Safe(excerpt.Text).Trim();
            return text.Length == 0 ? null : excerpt.TotalCharacters > excerpt.Text.Length ? text + "…" : text;
        }
        catch (LocalFileException)
        {
            // No file yet, or one that does not read: the project is known by its name and its folder.
            return null;
        }
    }

    /// <summary>The path of the file of the memory of a project.</summary>
    /// <param name="project">The project.</param>
    /// <returns>The path.</returns>
    public static string MemoryPath(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var folder = project.Folder.TrimEnd('/', '\\');
        return folder + (project.Folder.Contains('\\', StringComparison.Ordinal) ? '\\' : '/') + IProjectCatalog.MemoryFileName;
    }

    /// <summary>Creates a project.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="folder">Its local folder.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>What happened.</returns>
    public async ValueTask<ProjectOutcome> AddAsync(string? name, string? folder, CancellationToken cancellationToken = default)
    {
        var clean = (name ?? string.Empty).Trim();
        if (clean.Length is 0 or > ProjectsOptions.MostNameCharacters || Plain(clean).Length == 0)
        {
            return ProjectOutcome.BadName;
        }

        var path = (folder ?? string.Empty).Trim().Trim('"');
        if (path.Length == 0 || (files is not null && files.Resolve(path) is null))
        {
            return ProjectOutcome.BadFolder;
        }

        var options = settings.Read(ProjectsOptions.Section);
        var known = options.Projects ?? [];
        if (known.Any(project => Plain(project.Name) == Plain(clean)))
        {
            return ProjectOutcome.NameTaken;
        }

        if (known.Count >= ProjectsOptions.MostProjects)
        {
            return ProjectOutcome.Full;
        }

        // The identity comes from the first name and stays: a later rename would not lose what points to it.
        var id = Plain(clean);
        for (var n = 2; known.Any(project => project.Id == id); n++)
        {
            id = Plain(clean) + "-" + n.ToString(CultureInfo.InvariantCulture);
        }

        var result = await settings.WriteAsync(ProjectsOptions.Section, options with { Projects = [.. known, new ProjectEntry { Id = id, Name = clean, Folder = path }] }, cancellationToken).ConfigureAwait(false);
        return result.Outcome == SettingsWriteOutcome.Saved ? ProjectOutcome.Added : ProjectOutcome.NotSaved;
    }

    /// <summary>Removes a project. Its folder and its memory file are left as they are.</summary>
    /// <param name="id">Its identity.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether the settings were written.</returns>
    public async ValueTask<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        var options = settings.Read(ProjectsOptions.Section);
        var result = await settings.WriteAsync(ProjectsOptions.Section, options with { Projects = [.. (options.Projects ?? []).Where(project => project.Id != id)] }, cancellationToken).ConfigureAwait(false);
        return result.Outcome == SettingsWriteOutcome.Saved;
    }

    /// <inheritdoc />
    public void Dispose() => watch.Dispose();

    /// <summary>Lower case, without accents, words joined by hyphens: « Kazo OS » and « kazo os » are one name.</summary>
    internal static string Plain(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }

    /// <summary>A fixed text of the projects, in <paramref name="language"/>.</summary>
    internal static string Text(string key, AssistantLanguage language, params object[] arguments)
    {
        var text = Texts.GetString(key, language.Culture) ?? key;
        return arguments.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, arguments);
    }

    // No bracket of a file or of a name closes the block the model reads as data.
    private static string Safe(string text) => text.Replace('[', '(').Replace(']', ')');
}

/// <summary>Marker of the resources <c>ProjectTexts.resx</c> and <c>ProjectTexts.fr.resx</c>.</summary>
public sealed class ProjectTexts
{
    private ProjectTexts()
    {
    }
}
