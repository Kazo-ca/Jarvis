using Kazo.Jarvis.Updater.Installing;

namespace Kazo.Jarvis.Tests.Unit.Updater;

/// <summary>The command line of Kazo.Jarvis.Updater (its plain .NET code is compiled into this project).</summary>
[TestFixture]
public sealed class UpdaterArgumentsTests
{
    private static readonly string Own = Path.Combine(Path.GetTempPath(), "prepared", "content");

    [Test]
    public void Reads_what_the_application_passes()
    {
        var install = Path.Combine(Path.GetTempPath(), "Kazo.Jarvis");

        var arguments = UpdaterArguments.Parse(["--install-dir", install + Path.DirectorySeparatorChar, "--wait-pid", "4242"], Own);

        Assert.That(arguments, Is.EqualTo(new UpdaterArguments(install, Own, 4242, StartAfterUpdate: true)));
    }

    [Test]
    public void The_source_and_the_restart_can_be_given()
    {
        var source = Path.Combine(Path.GetTempPath(), "elsewhere");

        var arguments = UpdaterArguments.Parse(["--install-dir", "x", "--source", source, "--no-start"], Own);

        Assert.Multiple(() =>
        {
            Assert.That(arguments.SourceFolder, Is.EqualTo(source));
            Assert.That(arguments.StartAfterUpdate, Is.False);
            Assert.That(arguments.WaitProcessId, Is.Null);
        });
    }

    [TestCaseSource(nameof(WrongCommandLines))]
    public void Refuses_a_wrong_command_line(string[] args) =>
        Assert.Throws<ArgumentException>(() => UpdaterArguments.Parse(args, Own));

    private static IEnumerable<string[]> WrongCommandLines()
    {
        yield return [];
        yield return ["--install-dir"];
        yield return ["--install-dir", "--no-start"];
        yield return ["--install-dir", "x", "--wait-pid", "abc"];
        yield return ["--install-dir", "x", "--wait-pid", "-1"];
        yield return ["--install-dir", "x", "--tag", "v1"];
    }
}
