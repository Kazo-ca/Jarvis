using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Projects.Catalog;

/// <summary>The projects the user created (ADR 0025, section 6), kept in the settings.</summary>
public sealed record ProjectsOptions
{
    /// <summary>Most projects.</summary>
    public const int MostProjects = 50;

    /// <summary>Longest name of a project, in characters.</summary>
    public const int MostNameCharacters = 60;

    /// <summary>The section of the settings.</summary>
    public static SettingsSection<ProjectsOptions> Section { get; } = new("Projects.List", () => new ProjectsOptions(), Validate);

    /// <summary>The projects, in the order they were created.</summary>
    public IReadOnlyList<ProjectEntry> Projects { get; init; } = [];

    private static IReadOnlyList<string> Validate(ProjectsOptions value)
    {
        if (value.Projects is null)
        {
            return ["The list of projects is missing."];
        }

        var problems = new List<string>();
        if (value.Projects.Count > MostProjects)
        {
            problems.Add($"At most {MostProjects} projects.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in value.Projects)
        {
            if (project is null || string.IsNullOrWhiteSpace(project.Id) || string.IsNullOrWhiteSpace(project.Folder)
                || string.IsNullOrWhiteSpace(project.Name) || project.Name.Length > MostNameCharacters || ProjectCatalog.Plain(project.Name).Length == 0)
            {
                problems.Add($"A project needs an identity, a folder and a name of 1 to {MostNameCharacters} characters with a letter or a digit.");
                continue;
            }

            if (!ids.Add(project.Id) || !names.Add(ProjectCatalog.Plain(project.Name)))
            {
                problems.Add($"Two projects are named '{project.Name}'.");
            }
        }

        return problems;
    }
}

/// <summary>One project as the settings keep it.</summary>
public sealed record ProjectEntry
{
    /// <summary>Its identity, which never changes.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The name the user gave it.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Its local folder.</summary>
    public string Folder { get; init; } = string.Empty;
}
