using Kazo.Jarvis.Kernel.Localization;

namespace Kazo.Jarvis.Kernel.Projects;

/// <summary>
/// A project (ADR 0025, section 6): a name and a local folder, created in the interface of Jarvis. Creating one
/// writes no agent: the one « Projet » agent is loaded with the folder and the memory of the project asked.
/// </summary>
/// <param name="Id">Its identity, which never changes.</param>
/// <param name="Name">The name the user gave it, said and shown.</param>
/// <param name="Folder">Its local folder.</param>
public sealed record ProjectDefinition(string Id, string Name, string Folder);

/// <summary>
/// The projects, as the conversation and the agents need them (ADR 0025, sections 6, 10 and 11). The slice of the
/// projects answers; without one, a turn is about no project.
/// </summary>
public interface IProjectCatalog
{
    /// <summary>The name of the file of the memory of a project, at the root of its folder.</summary>
    const string MemoryFileName = "JARVIS.md";

    /// <summary>Raised when the projects change, on any thread.</summary>
    /// <summary>The key of the settings page where the user creates the projects and assigns the folders.</summary>
    const string SettingsPageKey = "Projects.Catalog";

    event Action? Changed;

    /// <summary>The projects, in the order the user created them.</summary>
    IReadOnlyList<ProjectDefinition> Projects { get; }

    /// <summary>The project of this name or identity, whatever the case and the accents.</summary>
    /// <param name="name">A name or an identity.</param>
    /// <returns>The project, or <see langword="null"/>.</returns>
    ProjectDefinition? Find(string? name);

    /// <summary>The project a request names in its words (« Dans Kazo Os, … »), the longest name first.</summary>
    /// <param name="request">The request as the user made it.</param>
    /// <returns>The project, or <see langword="null"/> when the request names none.</returns>
    ProjectDefinition? NamedIn(string? request);

    /// <summary>
    /// Whether a folder where a session works belongs to no project (ADR 0025, section 6): the user is then asked
    /// to assign it to a project or to create one, on the page <see cref="SettingsPageKey"/>.
    /// </summary>
    /// <param name="folder">A local path.</param>
    /// <returns><see langword="true"/> when the folder is known and no project holds it.</returns>
    bool IsWithoutProject(string? folder) => false;

    /// <summary>
    /// The path of the memory file of the project that holds a folder, so that a session started in that folder
    /// can be told to read it (ADR 0025, section 6). The file may not exist yet.
    /// </summary>
    /// <param name="folder">A local path.</param>
    /// <returns>The path, or <see langword="null"/> when no project holds the folder.</returns>
    string? MemoryFileOf(string? folder) => null;

    /// <summary>
    /// What an agent is told of a project when the turn is about it: its name, its folder and its memory, the
    /// Markdown file <see cref="MemoryFileName"/> of the folder. A delimited block that says of itself that it is
    /// data.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="language">The language of the turn.</param>
    /// <param name="contextLength">The context of the model that answers, in tokens.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The block.</returns>
    ValueTask<string> RecallAsync(ProjectDefinition project, AssistantLanguage language, int contextLength, CancellationToken cancellationToken = default);
}
