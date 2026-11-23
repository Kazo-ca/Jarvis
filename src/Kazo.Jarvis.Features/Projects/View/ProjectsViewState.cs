namespace Kazo.Jarvis.Features.Projects.View;

/// <summary>
/// What the projects view shows: kept while Jarvis runs, so that the view opens again where the user left it
/// (ADR 0026, section 8, provisional).
/// </summary>
public sealed class ProjectsViewState
{
    /// <summary>The entry of the rail that lists what waits for the user, all projects together.</summary>
    public const string Attention = "_attention";

    /// <summary>The entry of the rail that shows one card per project.</summary>
    public const string All = "_all";

    /// <summary>Raised when the selection changed.</summary>
    public event Action? Changed;

    /// <summary>The entry of the rail: <see cref="Attention"/>, <see cref="All"/> or the identifier of a project.</summary>
    public string Rail { get; private set; } = All;

    /// <summary>The subject shown at the centre, or <see langword="null"/> for the first one of the project.</summary>
    public string? Subject { get; private set; }

    /// <summary>The task whose thread is open on the right, or <see langword="null"/>.</summary>
    public string? Task { get; private set; }

    /// <summary>Selects an entry of the rail.</summary>
    /// <param name="rail">The entry.</param>
    /// <param name="subject">A subject of the project to open with it.</param>
    /// <param name="task">A task whose thread to open with it.</param>
    public void Show(string rail, string? subject = null, string? task = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(rail);
        (Rail, Subject, Task) = (rail, subject, task);
        Changed?.Invoke();
    }

    /// <summary>Opens the thread of a task of the subject shown, or closes it.</summary>
    /// <param name="task">The task, or <see langword="null"/> to go back to the subject.</param>
    public void Open(string? task)
    {
        Task = task;
        Changed?.Invoke();
    }
}
