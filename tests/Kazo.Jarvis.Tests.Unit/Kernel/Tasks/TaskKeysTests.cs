using Kazo.Jarvis.Kernel.Tasks;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Tasks;

/// <summary>The project key and the topic key of a task (ADR 0013, section 6), and the names of the specification.</summary>
[TestFixture]
public sealed class TaskKeysTests
{
    [TestCase("https://github.com/Kazo-ca/Jarvis.git", "github.com/kazo-ca/jarvis")]
    [TestCase("https://github.com/Kazo-ca/Jarvis", "github.com/kazo-ca/jarvis")]
    [TestCase("git@github.com:Kazo-ca/Jarvis.git", "github.com/kazo-ca/jarvis")]
    [TestCase("ssh://git@github.com/Kazo-ca/Jarvis.git", "github.com/kazo-ca/jarvis")]
    [TestCase("  https://GITHUB.com/Kazo-ca/Jarvis/  ", "github.com/kazo-ca/jarvis")]
    [TestCase("https://dev.azure.com/kazo/Jarvis/_git/Jarvis", "dev.azure.com/kazo/jarvis/_git/jarvis")]
    public void Every_form_of_the_same_remote_gives_the_same_project(string remote, string expected) =>
        Assert.That(TaskKeys.Project(remote, @"C:\src\somewhere"), Is.EqualTo(expected));

    [Test]
    public void A_password_in_the_address_of_a_remote_never_enters_the_key() =>
        Assert.That(TaskKeys.Project("https://benoit:ghp_secret@github.com/Kazo-ca/Jarvis.git", "x"), Is.EqualTo("github.com/kazo-ca/jarvis"));

    [TestCase(null, @"C:\Users\demo\Notes", "Notes")]
    [TestCase("", "/home/demo/notes/", "notes")]
    [TestCase("not an address", @"C:\src\kazo-bar\", "kazo-bar")]
    [TestCase(null, "", "_inbox")]
    [TestCase(null, null, "_inbox")]
    public void Without_a_remote_the_project_is_the_name_of_the_folder(string? remote, string? folder, string expected) =>
        Assert.That(TaskKeys.Project(remote, folder), Is.EqualTo(expected));

    [TestCase("github.com/kazo-ca/jarvis", "jarvis")]
    [TestCase("Notes", "Notes")]
    public void The_name_of_a_project_is_the_last_part_of_its_key(string key, string expected) =>
        Assert.That(TaskKeys.ProjectName(key), Is.EqualTo(expected));

    [TestCase("stage2/11-tasks-and-sessions", "branch:stage2/11-tasks-and-sessions")]
    [TestCase("main", "_inbox")]
    [TestCase("master", "_inbox")]
    [TestCase(null, "_inbox")]
    [TestCase("  ", "_inbox")]
    public void The_topic_is_the_branch_and_the_main_branch_is_the_inbox(string? branch, string expected) =>
        Assert.That(TaskKeys.Topic(branch), Is.EqualTo(expected));

    [Test]
    public void The_statuses_are_the_nine_of_the_specification_and_no_other() =>
        Assert.That(Enum.GetValues<WorkTaskStatus>().Select(status => status.ToWire()), Is.EqualTo(new[]
        {
            "pending", "working", "paused", "idle", "waiting_answer", "waiting_approval", "blocked", "done", "abandoned",
        }));

    [Test]
    public void The_sources_are_those_of_the_specification() =>
        Assert.That(Enum.GetValues<TaskSource>().Select(source => source.ToWire()), Is.EqualTo(new[] { "harness", "hook", "mcp", "orchestrator" }));

    [Test]
    public void Only_the_two_waiting_statuses_wait_for_the_human_and_only_two_end_a_task()
    {
        WorkTask With(WorkTaskStatus status) => new("t", null, "p", "_inbox", "Titre", null, new TaskOperator(TaskOperator.ClaudeCode), TaskSource.Harness, status, default, default);

        Assert.Multiple(() =>
        {
            Assert.That(Enum.GetValues<WorkTaskStatus>().Where(status => With(status).WaitsForHuman), Is.EqualTo(new[] { WorkTaskStatus.WaitingAnswer, WorkTaskStatus.WaitingApproval }));
            Assert.That(Enum.GetValues<WorkTaskStatus>().Where(status => With(status).Ended), Is.EqualTo(new[] { WorkTaskStatus.Done, WorkTaskStatus.Abandoned }));
        });
    }
}
