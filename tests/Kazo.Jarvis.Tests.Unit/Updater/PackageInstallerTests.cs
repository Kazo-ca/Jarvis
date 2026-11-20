using Kazo.Jarvis.Tests.Unit.Adapters;
using Kazo.Jarvis.Updater.Installing;

namespace Kazo.Jarvis.Tests.Unit.Updater;

/// <summary>Replacing the files of an installation (Kazo.Jarvis.Updater, compiled into this project).</summary>
[TestFixture]
public sealed class PackageInstallerTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 3, 4, 5, DateTimeKind.Utc);
    private TemporaryFolder folder = null!;
    private string install = null!;
    private string source = null!;
    private List<string> log = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        install = Path.Combine(folder.Path, "install");
        source = Path.Combine(folder.Path, "updates", "Kazo.Jarvis-0.2.0-win-x64", "content");
        log = [];
        Write(install, "Kazo.Jarvis.Windows.exe", "old");
        Write(install, "old-only.dll", "old");
        Write(install, Path.Combine("fr", "Kazo.Jarvis.Features.resources.dll"), "old");
        Write(source, "Kazo.Jarvis.Windows.exe", "new");
        Write(source, "Kazo.Jarvis.Updater.exe", "new");
        Write(source, Path.Combine("fr", "Kazo.Jarvis.Features.resources.dll"), "new");
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public void The_new_version_replaces_every_file_and_the_old_one_is_kept_aside()
    {
        var backup = new PackageInstaller(log.Add).Apply(source, install, Now);

        Assert.Multiple(() =>
        {
            Assert.That(Read(install, "Kazo.Jarvis.Windows.exe"), Is.EqualTo("new"));
            Assert.That(Read(install, "Kazo.Jarvis.Updater.exe"), Is.EqualTo("new"));
            Assert.That(Read(install, Path.Combine("fr", "Kazo.Jarvis.Features.resources.dll")), Is.EqualTo("new"));
            Assert.That(File.Exists(Path.Combine(install, "old-only.dll")), Is.False, "files the new version dropped go away");
            Assert.That(Path.GetFileName(backup), Is.EqualTo(".kazo-update-backup-20261120030405"));
            Assert.That(Read(backup, "old-only.dll"), Is.EqualTo("old"));
            Assert.That(Read(source, "Kazo.Jarvis.Windows.exe"), Is.EqualTo("new"), "the source is copied, not moved");
        });
    }

    [Test]
    public void A_failure_restores_the_previous_version()
    {
        // A folder in the way of a file of the new version stops the copy half way, on Windows as on Linux.
        Write(source, Path.Combine("zz", "a.dll"), "new");
        Directory.CreateDirectory(Path.Combine(install, "zz", "a.dll"));

        Assert.That(() => new PackageInstaller(log.Add).Apply(source, install, Now), Throws.Exception);

        Assert.Multiple(() =>
        {
            Assert.That(Read(install, "Kazo.Jarvis.Windows.exe"), Is.EqualTo("old"));
            Assert.That(Read(install, "old-only.dll"), Is.EqualTo("old"));
            Assert.That(File.Exists(Path.Combine(install, "Kazo.Jarvis.Updater.exe")), Is.False);
            Assert.That(log.Any(line => line.StartsWith("Install failed", StringComparison.Ordinal)), Is.True);
        });
    }

    [Test]
    public void Earlier_backups_are_deleted_and_never_moved_into_the_next_one()
    {
        var first = new PackageInstaller(log.Add).Apply(source, install, Now);

        PackageInstaller.DeleteBackups(install, log.Add);

        Assert.That(Directory.Exists(first), Is.False);
    }

    [Test]
    public void A_safe_pair_of_folders_is_accepted() =>
        Assert.DoesNotThrow(() => PackageInstaller.EnsureSafe(install, source, Path.Combine(folder.Path, ".kazo", "jarvis")));

    [Test]
    public void Refuses_a_folder_without_the_application()
    {
        File.Delete(Path.Combine(install, "Kazo.Jarvis.Windows.exe"));

        Assert.Throws<InvalidOperationException>(() => PackageInstaller.EnsureSafe(install, source, Path.Combine(folder.Path, "data")));
    }

    [Test]
    public void Refuses_a_source_without_the_application()
    {
        File.Delete(Path.Combine(source, "Kazo.Jarvis.Windows.exe"));

        Assert.Throws<InvalidOperationException>(() => PackageInstaller.EnsureSafe(install, source, Path.Combine(folder.Path, "data")));
    }

    [Test]
    public void Refuses_an_installation_overlapping_the_user_data() =>
        Assert.Throws<InvalidOperationException>(() => PackageInstaller.EnsureSafe(install, source, folder.Path));

    [Test]
    public void Refuses_a_source_inside_the_installation()
    {
        var inside = Path.Combine(install, "new");
        Write(inside, "Kazo.Jarvis.Windows.exe", "new");

        Assert.Throws<InvalidOperationException>(() => PackageInstaller.EnsureSafe(install, inside, Path.Combine(folder.Path, "data")));
    }

    [Test]
    public void Refuses_a_drive_root() =>
        Assert.Throws<InvalidOperationException>(() => PackageInstaller.EnsureSafe(Path.GetPathRoot(install)!, source, Path.Combine(folder.Path, "data")));

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string Read(string root, string relative) => File.ReadAllText(Path.Combine(root, relative));
}
