using Kazo.Jarvis.Features.Projects.View;
using Kazo.Jarvis.Kernel.Projects;
using Kazo.Jarvis.Kernel.Tasks;

namespace Kazo.Jarvis.Tests.Unit.Features.Projects;

/// <summary>The subjects the user makes with « + Nouveau sujet » (work item stage2/29-new-subject; ADR 0026, section 11).</summary>
[TestFixture]
public sealed class UserSubjectTests
{
    private const string Jarvis = @"C:\dev\Kazo.Jarvis";
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void A_subject_gets_a_key_from_its_name_that_no_subject_of_the_project_has()
    {
        var first = UserSubject.Make("kazo-jarvis", "  Refonte du menu d'été ", [], Now)!;
        var second = UserSubject.Make("kazo-jarvis", "Refonte du menu d'été", [first.Key, "branch:main"], Now)!;

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new UserSubject { Project = "kazo-jarvis", Key = "subject:refonte-du-menu-d-ete", Name = "Refonte du menu d'été", MadeAt = Now }));
            Assert.That(second.Key, Is.EqualTo("subject:refonte-du-menu-d-ete-2"));
            Assert.That(UserSubject.Make("kazo-jarvis", "???", [], Now)!.Key, Is.EqualTo("subject:sujet"), "A name with no letter still gets a key.");
            Assert.That(UserSubject.Make("kazo-jarvis", "   ", [], Now), Is.Null);
            Assert.That(UserSubject.Make("kazo-jarvis", new string('a', 61), [], Now), Is.Null);
        });
    }

    [Test]
    public void A_subject_the_user_made_is_shown_open_before_it_has_a_task_and_takes_the_tasks_of_its_key()
    {
        var catalog = new ProjectsOverviewTests.Catalog(new ProjectDefinition("kazo-jarvis", "Kazo.Jarvis", Jarvis));
        UserSubject[] made =
        [
            new() { Project = "kazo-jarvis", Key = "subject:menu", Name = "Refonte du menu", MadeAt = Now },
            new() { Project = "kazo-jarvis", Key = "subject:pied", Name = "Pied de page", MadeAt = Now.AddMinutes(-5) },
            new() { Project = ProjectsOverview.GeneralId, Key = "subject:courriels", Name = "Courriels", MadeAt = Now },
            new() { Project = "gone", Key = "subject:x", Name = "X", MadeAt = Now },
        ];

        var overview = ProjectsOverview.From(
            [ProjectsOverviewTests.Task("a", "corrige le pied", WorkTaskStatus.Done, 0, 1, topic: "subject:pied", folder: Jarvis)],
            catalog,
            made);

        var project = overview.Find("kazo-jarvis")!;
        var empty = project.Subjects.Single(subject => subject.Key == "subject:menu");
        var withTask = project.Subjects.Single(subject => subject.Key == "subject:pied");
        Assert.Multiple(() =>
        {
            Assert.That(project.Subjects, Has.Count.EqualTo(2));
            Assert.That((empty.Name, empty.Ended, empty.Steps.Count, empty.UpdatedAt, empty.Lines.Count), Is.EqualTo(("Refonte du menu", false, 0, Now, 0)));
            Assert.That((withTask.Name, withTask.Ended, withTask.Steps.Single().Label), Is.EqualTo(("Pied de page", true, "T1")), "Its tasks are those of its key, and it ends like any other.");
            Assert.That(project.Subjects[0].Key, Is.EqualTo("subject:menu"), "Open subjects first.");
            Assert.That(overview.General.Subjects.Single().Name, Is.EqualTo("Courriels"));
            Assert.That(overview.Projects.SelectMany(item => item.Subjects).Any(subject => subject.Key == "subject:x"), Is.False, "A subject of a project that is gone is not shown.");
            Assert.That(ProjectOffer.For(overview, [], new ProjectsViewOptions { OfferTasks = 1 }, Now), Is.Empty, "A subject with no folder is never offered as a project.");
        });
    }
}
