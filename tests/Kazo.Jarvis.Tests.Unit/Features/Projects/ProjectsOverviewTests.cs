using Kazo.Jarvis.Features.Projects.View;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Projects;
using Kazo.Jarvis.Kernel.Tasks;

namespace Kazo.Jarvis.Tests.Unit.Features.Projects;

/// <summary>What the projects view shows from the task board (work item stage2/24-projects-view; ADR 0026).</summary>
[TestFixture]
public sealed class ProjectsOverviewTests
{
    private const string Jarvis = @"C:\dev\Kazo.Jarvis";
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 5, 17, 0, TimeSpan.Zero);

    [Test]
    public void The_tasks_of_a_project_are_grouped_by_subject_and_numbered_in_the_order_they_were_created()
    {
        var catalog = new Catalog(new ProjectDefinition("kazo-jarvis", "Kazo.Jarvis", Jarvis), new ProjectDefinition("kazo-os", "Kazo OS", @"C:\dev\Os"));
        var overview = ProjectsOverview.From(
        [
            Task("c", "Préparation documentation", WorkTaskStatus.WaitingAnswer, 4, 11, topic: "branch:vue-projets", folder: Jarvis),
            Task("a", "Lire l'image", WorkTaskStatus.Done, 0, 1, topic: "branch:vue-projets", folder: Jarvis, summary: "3 colonnes."),
            Task("b", "Créer une maquette", WorkTaskStatus.Done, 1, 3, topic: "branch:vue-projets", folder: Jarvis),
            Task("d", "Écrire l'ADR", WorkTaskStatus.Pending, 5, 5, topic: "branch:vue-projets", folder: Jarvis),
            Task("c1", "Poser des questions", WorkTaskStatus.Working, 6, 7, topic: "branch:vue-projets", folder: Jarvis, parent: "c"),
            Task("c0", "Résumer les décisions", WorkTaskStatus.Done, 5, 6, topic: "branch:vue-projets", folder: Jarvis, parent: "c"),
            Task("e", "Plafond de dépenses", WorkTaskStatus.Abandoned, 0, 2, topic: "branch:spend", folder: Jarvis + @"\src"),
        ], catalog);

        var project = overview.Find("kazo-jarvis")!;
        var subject = project.Subjects[0];
        Assert.Multiple(() =>
        {
            Assert.That(overview.Projects.Select(item => item.Id), Is.EqualTo(new[] { "kazo-jarvis", "kazo-os", ProjectsOverview.GeneralId }), "The built-in project comes last.");
            Assert.That(project.Subjects.Select(item => (item.Name, item.Ended)), Is.EqualTo(new[] { ("vue-projets", false), ("spend", true) }), "The subjects in progress first.");
            Assert.That(subject.Steps.Select(step => (step.Label, step.Task.Id, step.Kind)), Is.EqualTo(new[]
            {
                ("T1", "a", StepKind.Done), ("T2", "b", StepKind.Done), ("T3", "c", StepKind.Active), ("T4", "d", StepKind.Planned),
            }));
            Assert.That((subject.Done, subject.Active, subject.Planned, subject.Waiting), Is.EqualTo((2, 1, 1, 1)));
            Assert.That(subject.Steps[2].SubTasks.Select(child => child.Id), Is.EqualTo(new[] { "c0", "c1" }), "A sub-task is never a task of the subject.");
            Assert.That(subject.Steps[2].SubTasksEnded, Is.EqualTo(1));
            Assert.That(project.Subjects[1].Steps.Single().Kind, Is.EqualTo(StepKind.Stopped));
            Assert.That(overview.Find("kazo-os")!.Subjects, Is.Empty, "A project with no task is still shown.");
            Assert.That(overview.Waiting, Is.EqualTo(1));
            Assert.That(overview.Attention.Single().Step.Task.Id, Is.EqualTo("c"));
            Assert.That(overview.FoldersWithoutProject, Is.Empty);
        });
    }

    [Test]
    public void The_thread_of_a_subject_tells_the_start_and_the_end_of_each_task()
    {
        var overview = ProjectsOverview.From(
        [
            Task("a", "Lire l'image", WorkTaskStatus.Done, 0, 1, folder: Jarvis),
            Task("b", "Créer une maquette", WorkTaskStatus.Working, 1, 3, folder: Jarvis),
            Task("c", "Poser des questions", WorkTaskStatus.WaitingAnswer, 2, 4, folder: Jarvis),
        ], new Catalog(new ProjectDefinition("kazo-jarvis", "Kazo.Jarvis", Jarvis)));

        var project = overview.Projects[0];
        Assert.Multiple(() =>
        {
            Assert.That(project.Subjects.Single().Name, Is.Null, "A task on the main branch has no subject of its own.");
            Assert.That(project.Subjects.Single().Lines.Select(line => (line.Step.Label, line.Kind)), Is.EqualTo(new[]
            {
                ("T1", ThreadLineKind.Opened), ("T1", ThreadLineKind.Ended), ("T2", ThreadLineKind.Opened), ("T3", ThreadLineKind.Opened), ("T3", ThreadLineKind.Waiting),
            }));
            Assert.That(project.Last?.Line.Step.Label, Is.EqualTo("T3"), "The last word of the thread of the project.");
        });
    }

    [Test]
    public void What_belongs_to_no_project_goes_to_the_built_in_project_and_a_folder_where_a_session_works_is_asked_about()
    {
        var catalog = new Catalog(new ProjectDefinition("kazo-jarvis", "Kazo.Jarvis", Jarvis));
        var overview = ProjectsOverview.From(
        [
            Task("a", "corrige le pied de page", WorkTaskStatus.Working, 0, 1, project: "kazo-bar", folder: @"C:\dev\Bar"),
            Task("b", "ajoute un test", WorkTaskStatus.Done, 1, 2, project: "kazo-bar", folder: @"C:\dev\Bar"),
            Task("c", "range les notes", WorkTaskStatus.Done, 1, 2, project: "notes", folder: @"C:\Notes"),
            Task("d", "météo de demain", WorkTaskStatus.Done, 2, 3),
            Task("orphan", "sous-tâche sans tâche", WorkTaskStatus.Working, 2, 3, parent: "gone"),
        ], catalog);
        var withoutCatalog = ProjectsOverview.From([Task("a", "x", WorkTaskStatus.Working, 0, 1, folder: Jarvis)], null);

        Assert.Multiple(() =>
        {
            Assert.That(overview.General.General, Is.True);
            Assert.That(overview.General.Subjects.Select(subject => subject.Name), Is.EquivalentTo(new[] { "kazo-bar", "notes", null }));
            Assert.That(overview.General.Subjects.First(subject => subject.Name == "kazo-bar").Steps.Select(step => step.Task.Id), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(overview.FoldersWithoutProject, Is.EqualTo(new[] { @"C:\dev\Bar" }), "Only a folder where a session still works.");
            Assert.That(overview.General.Subjects.SelectMany(subject => subject.Steps).Select(step => step.Task.Id), Does.Contain("orphan"));
            Assert.That(withoutCatalog.Projects.Single().Subjects.Single().Steps, Has.Count.EqualTo(1), "A host with no catalog has the built-in project only.");
            Assert.That(withoutCatalog.FoldersWithoutProject, Is.Empty);
        });
    }

    private static WorkTask Task(string id, string title, WorkTaskStatus status, int created, int updated, string project = TaskKeys.Inbox, string topic = TaskKeys.Inbox, string? folder = null, string? parent = null, string? summary = null) =>
        new(id, parent, project, topic, title, summary, new TaskOperator(TaskOperator.ClaudeCode), TaskSource.Hook, status, Start.AddMinutes(created), Start.AddMinutes(updated), folder);

    private sealed class Catalog(params ProjectDefinition[] projects) : IProjectCatalog
    {
        public event Action? Changed
        {
            add { }
            remove { }
        }

        public IReadOnlyList<ProjectDefinition> Projects => projects;

        public ProjectDefinition? Find(string? name) => null;

        public ProjectDefinition? NamedIn(string? request) => null;

        public ProjectDefinition? OwnerOf(string? folder) =>
            folder is null ? null : projects.FirstOrDefault(project => folder.StartsWith(project.Folder, StringComparison.OrdinalIgnoreCase));

        public bool IsWithoutProject(string? folder) => !string.IsNullOrWhiteSpace(folder) && OwnerOf(folder) is null;

        public ValueTask<string> RecallAsync(ProjectDefinition project, AssistantLanguage language, int contextLength, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(string.Empty);
    }
}
