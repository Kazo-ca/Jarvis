using Kazo.Jarvis.Features.Projects.View;
using Kazo.Jarvis.Kernel.Conversation;
using Kazo.Jarvis.Kernel.Projects;
using Kazo.Jarvis.Kernel.Tasks;

namespace Kazo.Jarvis.Tests.Unit.Features.Projects;

/// <summary>
/// The offer to make a project of a subject of « Général », and where the messages of a thread are shown once
/// its tasks moved (work item stage2/28-general-project-offer; ADR 0026, sections 6 and 11).
/// </summary>
[TestFixture]
public sealed class ProjectOfferTests
{
    private const string Bar = @"C:\dev\kazo-bar";
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 5, 17, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Start.AddHours(2);

    private static WorkTask Task(string id, int created, string project = "kazo-bar", string topic = "branch:main", string? folder = Bar) =>
        ProjectsOverviewTests.Task(id, "tâche " + id, WorkTaskStatus.Done, created, created + 1, project, topic, folder);

    private static ThreadMessage Message(int minute, string project, string subject, string? task = null) =>
        new(Guid.NewGuid(), Start.AddMinutes(minute), "message " + minute, "réponse", null, new ThreadAddress(project, subject, task), false);

    private static ProjectsOverview General(params WorkTask[] tasks) => ProjectsOverview.From(tasks, new ProjectsOverviewTests.Catalog());

    [Test]
    public void A_subject_of_the_built_in_project_with_four_tasks_in_a_week_is_offered_with_its_name_and_its_folder()
    {
        var three = General(Task("a", 0), Task("b", 1), Task("c", 2));
        var four = General(Task("a", 0), Task("b", 1), Task("c", 2), Task("d", 3, folder: Bar + @"\src"), Task("e", 4, project: "notes", folder: @"C:\notes"));

        var offer = ProjectOffer.For(four, [], new ProjectsViewOptions(), Now).Single();
        Assert.Multiple(() =>
        {
            Assert.That(ProjectOffer.For(three, [], new ProjectsViewOptions(), Now), Is.Empty);
            Assert.That(offer, Is.EqualTo(new ProjectOffer("kazo-bar", "kazo-bar", Bar, 4, 0)), "The folder most of its tasks work in.");
            Assert.That(ProjectOffer.For(four, [], new ProjectsViewOptions(), Now.AddDays(8)), Is.Empty, "Only the tasks of the last days count.");
            Assert.That(ProjectOffer.For(four, [], new ProjectsViewOptions { OfferTasks = 5 }, Now), Is.Empty, "The threshold can be set.");
        });
    }

    [Test]
    public void Thirty_messages_in_a_week_are_enough_and_a_subject_with_no_folder_is_never_offered()
    {
        var overview = General(Task("a", 0), Task("n", 0, project: "notes", folder: null));
        var messages = Enumerable.Range(1, 30).Select(minute => Message(minute, ProjectsOverview.GeneralId, "kazo-bar")).ToList();
        var toNotes = Enumerable.Range(1, 30).Select(minute => Message(minute, ProjectsOverview.GeneralId, "notes")).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ProjectOffer.For(overview, messages, new ProjectsViewOptions(), Now).Single(), Is.EqualTo(new ProjectOffer("kazo-bar", "kazo-bar", Bar, 1, 30)));
            Assert.That(ProjectOffer.For(overview, messages.Take(29).ToList(), new ProjectsViewOptions(), Now), Is.Empty);
            Assert.That(ProjectOffer.For(overview, toNotes, new ProjectsViewOptions(), Now), Is.Empty, "Nowhere to make the project.");
        });
    }

    [Test]
    public void An_offer_refused_never_comes_back_and_an_offer_put_off_comes_back_after_its_delay()
    {
        var overview = General(Task("a", 0), Task("b", 1), Task("c", 2), Task("d", 3));
        var refused = new ProjectsViewOptions { Refused = ["kazo-bar"] };
        var later = new ProjectsViewOptions { Later = new Dictionary<string, DateTimeOffset> { ["kazo-bar"] = Now.AddDays(1) } };

        Assert.Multiple(() =>
        {
            Assert.That(ProjectOffer.For(overview, [], refused, Now.AddDays(3)), Is.Empty);
            Assert.That(ProjectOffer.For(overview, [], later, Now), Is.Empty);
            Assert.That(ProjectOffer.For(overview, [], later, Now.AddDays(2)), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Once_the_folder_has_a_project_the_messages_follow_the_tasks()
    {
        WorkTask[] tasks = [Task("a", 0, topic: "branch:menu"), Task("b", 10, topic: "branch:footer")];
        var before = General(tasks);
        var after = ProjectsOverview.From(tasks, new ProjectsOverviewTests.Catalog(new ProjectDefinition("kazo-bar", "Kazo Bar", Bar)));
        var early = Message(5, ProjectsOverview.GeneralId, "kazo-bar");
        var late = Message(15, ProjectsOverview.GeneralId, "kazo-bar");
        var first = Message(-5, ProjectsOverview.GeneralId, "kazo-bar");
        var toTask = Message(20, ProjectsOverview.GeneralId, "kazo-bar", task: "a");
        var elsewhere = Message(20, ProjectsOverview.GeneralId, "notes");
        ThreadMessage[] all = [early, late, first, toTask, elsewhere];
        var project = after.Find("kazo-bar")!;
        var menu = project.Subjects.Single(subject => subject.Key == "branch:menu");
        var footer = project.Subjects.Single(subject => subject.Key == "branch:footer");

        Assert.Multiple(() =>
        {
            Assert.That(ThreadHomes.Of(before, before.General, before.General.Subjects.Single(), all), Is.EqualTo(new[] { first, early, late, toTask }), "Nothing moved: oldest first.");
            Assert.That(ThreadHomes.Of(after, project, menu, all), Is.EqualTo(new[] { first, early, toTask }), "With the task that was the newest when it was written; an answer to a task with its task.");
            Assert.That(ThreadHomes.Of(after, project, footer, all), Is.EqualTo(new[] { late }));
            Assert.That(ThreadHomes.HomeOf(after, elsewhere), Is.EqualTo((ProjectsOverview.GeneralId, "notes")), "A message whose subject has no task stays where it was written.");
        });
    }
}
