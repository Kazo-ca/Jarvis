using Kazo.Jarvis.Kernel.Projects;
using Kazo.Jarvis.Kernel.Tasks;

namespace Kazo.Jarvis.Features.Projects.View;

/// <summary>Where a task of a subject stands (ADR 0026, section 3).</summary>
public enum StepKind
{
    /// <summary>Done: its thread can be read again.</summary>
    Done,

    /// <summary>In progress: always visible.</summary>
    Active,

    /// <summary>Planned: not started yet.</summary>
    Planned,

    /// <summary>Stopped before its end.</summary>
    Stopped,
}

/// <summary>A task of a subject, numbered in the order the tasks were created.</summary>
/// <param name="Number">Its number in the subject, from 1: <c>T1</c>, <c>T2</c>…</param>
/// <param name="Task">The task of the board.</param>
/// <param name="SubTasks">Its sub-tasks, oldest first. Read only: a sub-task has no thread (section 1).</param>
public sealed record TaskStep(int Number, WorkTask Task, IReadOnlyList<WorkTask> SubTasks)
{
    /// <summary>The label written in the thread of the subject.</summary>
    public string Label => "T" + Number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Where the task stands.</summary>
    public StepKind Kind => Task.Status switch
    {
        WorkTaskStatus.Done => StepKind.Done,
        WorkTaskStatus.Abandoned => StepKind.Stopped,
        WorkTaskStatus.Pending => StepKind.Planned,
        _ => StepKind.Active,
    };

    /// <summary>Whether the task, or one of its sub-tasks, waits for the user.</summary>
    public bool Waits => Task.WaitsForHuman || SubTasks.Any(child => child.WaitsForHuman);

    /// <summary>How many sub-tasks are over.</summary>
    public int SubTasksEnded => SubTasks.Count(child => child.Ended);
}

/// <summary>What a line of the thread of a subject tells.</summary>
public enum ThreadLineKind
{
    /// <summary>A task started.</summary>
    Opened,

    /// <summary>A task waits for the user.</summary>
    Waiting,

    /// <summary>A task ended, with its summary.</summary>
    Ended,
}

/// <summary>
/// A line of the thread of a subject. Until the messages of a subject are kept, the thread holds what the board
/// knows: the start and the end of each task (ADR 0026, section 11, point 1).
/// </summary>
/// <param name="At">When it happened.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Step">The task it is about.</param>
public sealed record ThreadLine(DateTimeOffset At, ThreadLineKind Kind, TaskStep Step);

/// <summary>A subject of a project, with its tasks in order.</summary>
/// <param name="Key">Its key in the project.</param>
/// <param name="Name">Its name, or <see langword="null"/> for what has no subject.</param>
/// <param name="Steps">Its tasks, numbered.</param>
public sealed record SubjectBoard(string Key, string? Name, IReadOnlyList<TaskStep> Steps)
{
    /// <summary>How many tasks are done or stopped.</summary>
    public int Done => Steps.Count(step => step.Kind is StepKind.Done or StepKind.Stopped);

    /// <summary>How many tasks are in progress.</summary>
    public int Active => Steps.Count(step => step.Kind == StepKind.Active);

    /// <summary>How many tasks are planned.</summary>
    public int Planned => Steps.Count(step => step.Kind == StepKind.Planned);

    /// <summary>Whether every task of the subject is over.</summary>
    public bool Ended => Done == Steps.Count;

    /// <summary>How many tasks wait for the user.</summary>
    public int Waiting => Steps.Count(step => step.Waits);

    /// <summary>The last change of one of its tasks.</summary>
    public DateTimeOffset UpdatedAt => Steps.Max(step => step.Task.UpdatedAt);

    /// <summary>The thread of the subject, oldest line first.</summary>
    public IReadOnlyList<ThreadLine> Lines
    {
        get
        {
            var lines = new List<ThreadLine>();
            foreach (var step in Steps)
            {
                lines.Add(new ThreadLine(step.Task.CreatedAt, ThreadLineKind.Opened, step));
                if (step.Task.Ended)
                {
                    lines.Add(new ThreadLine(step.Task.UpdatedAt, ThreadLineKind.Ended, step));
                }
                else if (step.Task.WaitsForHuman)
                {
                    lines.Add(new ThreadLine(step.Task.UpdatedAt, ThreadLineKind.Waiting, step));
                }
            }

            // A stable sort: at the same instant a task starts before it ends, and T1 comes before T2.
            return [.. lines.OrderBy(line => line.At)];
        }
    }
}

/// <summary>A project as the view shows it.</summary>
/// <param name="Id">Its identifier in the catalog, or <see cref="ProjectsOverview.GeneralId"/>.</param>
/// <param name="Name">Its name; empty for the built-in project.</param>
/// <param name="Folder">Its main folder; <see langword="null"/> for the built-in project.</param>
/// <param name="Subjects">Its subjects: the ones in progress first, then the most recently changed.</param>
public sealed record ProjectBoard(string Id, string Name, string? Folder, IReadOnlyList<SubjectBoard> Subjects)
{
    /// <summary>Whether it is the built-in project of what belongs to no project (ADR 0026, section 6).</summary>
    public bool General => Id == ProjectsOverview.GeneralId;

    /// <summary>How many tasks wait for the user.</summary>
    public int Waiting => Subjects.Sum(subject => subject.Waiting);

    /// <summary>The newest line of all its subjects: the last word of the thread of the project.</summary>
    public (SubjectBoard Subject, ThreadLine Line)? Last =>
        Subjects.SelectMany(subject => subject.Lines.Select(line => (subject, line))).OrderBy(item => item.line.At).Cast<(SubjectBoard, ThreadLine)?>().LastOrDefault();
}

/// <summary>
/// The projects, their subjects and their tasks (ADR 0026), computed from the task board and the catalog of
/// projects. Pure: no clock, no storage.
/// </summary>
/// <param name="Projects">The projects of the catalog, in its order, then the built-in one last.</param>
/// <param name="FoldersWithoutProject">Folders where a session works and that belong to no project (ADR 0025, section 6).</param>
public sealed record ProjectsOverview(IReadOnlyList<ProjectBoard> Projects, IReadOnlyList<string> FoldersWithoutProject)
{
    /// <summary>The identifier of the built-in project.</summary>
    public const string GeneralId = "_general";

    /// <summary>How many tasks wait for the user, all projects together.</summary>
    public int Waiting => Projects.Sum(project => project.Waiting);

    /// <summary>The tasks that wait for the user, the longest wait first.</summary>
    public IReadOnlyList<(ProjectBoard Project, SubjectBoard Subject, TaskStep Step)> Attention =>
        [.. Projects
            .SelectMany(project => project.Subjects.SelectMany(subject => subject.Steps.Where(step => step.Waits).Select(step => (project, subject, step))))
            .OrderBy(item => item.step.Task.UpdatedAt)];

    /// <summary>The built-in project.</summary>
    public ProjectBoard General => Projects[^1];

    /// <summary>Finds a project.</summary>
    /// <param name="id">Its identifier.</param>
    /// <returns>The project, or <see langword="null"/>.</returns>
    public ProjectBoard? Find(string? id) => Projects.FirstOrDefault(project => project.Id == id);

    /// <summary>Reads the board.</summary>
    /// <param name="tasks">The tasks of the board.</param>
    /// <param name="catalog">The projects, when the host has them.</param>
    /// <returns>What the view shows.</returns>
    public static ProjectsOverview From(IReadOnlyList<WorkTask> tasks, IProjectCatalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var children = tasks.Where(task => task.ParentId is not null).ToLookup(task => task.ParentId!);
        var known = tasks.Select(task => task.Id).ToHashSet(StringComparer.Ordinal);
        var definitions = catalog?.Projects ?? [];
        var byProject = definitions.ToDictionary(project => project.Id, _ => new List<WorkTask>(), StringComparer.Ordinal);
        var general = new List<WorkTask>();
        var without = new List<string>();
        foreach (var task in tasks)
        {
            // A sub-task follows its task; one whose task is gone is shown as a task.
            if (task.ParentId is { } parent && known.Contains(parent))
            {
                continue;
            }

            if (catalog?.OwnerOf(task.Folder) is { } owner && byProject.TryGetValue(owner.Id, out var mine))
            {
                mine.Add(task);
                continue;
            }

            general.Add(task);
            if (!task.Ended && task.Folder is { } folder && catalog?.IsWithoutProject(folder) == true
                && !without.Contains(folder, StringComparer.OrdinalIgnoreCase))
            {
                without.Add(folder);
            }
        }

        var projects = definitions
            .Select(project => new ProjectBoard(project.Id, project.Name, project.Folder, Subjects(byProject[project.Id], children, task => task.Topic)))
            // Outside a project, the folder or the repository of a task is the closest thing to a subject.
            .Append(new ProjectBoard(GeneralId, string.Empty, null, Subjects(general, children, task => task.Project == TaskKeys.Inbox ? task.Topic : task.Project)))
            .ToList();
        return new ProjectsOverview(projects, without);
    }

    private static List<SubjectBoard> Subjects(List<WorkTask> tasks, ILookup<string, WorkTask> children, Func<WorkTask, string> key) =>
        [.. tasks
            .GroupBy(key, StringComparer.Ordinal)
            .Select(group => new SubjectBoard(
                group.Key,
                Name(group.Key),
                [.. group
                    .OrderBy(task => task.CreatedAt)
                    .ThenBy(task => task.Id, StringComparer.Ordinal)
                    .Select((task, index) => new TaskStep(index + 1, task, [.. children[task.Id].OrderBy(child => child.CreatedAt)]))]))
            .OrderBy(subject => subject.Ended)
            .ThenByDescending(subject => subject.UpdatedAt)];

    private static string? Name(string key)
    {
        if (key == TaskKeys.Inbox)
        {
            return null;
        }

        const string Branch = "branch:";
        return key.StartsWith(Branch, StringComparison.Ordinal) ? key[Branch.Length..] : TaskKeys.ProjectName(key);
    }
}
