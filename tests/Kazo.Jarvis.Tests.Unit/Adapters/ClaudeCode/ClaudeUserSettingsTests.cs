using Kazo.Jarvis.Adapters.ClaudeCode;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Adapters.ClaudeCode;

/// <summary>
/// The user's Claude Code settings file (ADR 0021, section 6), in a folder of the test: a write first makes a
/// dated copy of the file as it was, and leaves nothing else behind.
/// </summary>
[TestFixture]
public sealed class ClaudeUserSettingsTests
{
    private TemporaryFolder folder = null!;
    private string home = null!;
    private string path = null!;
    private FakeTimeProvider time = null!;
    private ClaudeUserSettings file = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        home = Path.Combine(folder.Path, ".claude");
        path = Path.Combine(home, "settings.json");
        time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 14, 5, 9, TimeSpan.Zero));
        file = new ClaudeUserSettings(home, time);
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public async Task A_file_that_does_not_exist_is_missing_and_nothing_is_created_by_reading()
    {
        var read = await file.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(read.State, Is.EqualTo(ClaudeUserSettingsState.Missing));
            Assert.That(read.Path, Is.EqualTo(path));
            Assert.That(read.Text, Is.Null);
            Assert.That(Directory.Exists(home), Is.False);
        });
    }

    [Test]
    public async Task The_file_is_read_as_it_is()
    {
        Directory.CreateDirectory(home);
        await File.WriteAllTextAsync(path, "{ \"model\": \"opus\" }");

        var read = await file.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(read.State, Is.EqualTo(ClaudeUserSettingsState.Read));
            Assert.That(read.Text, Is.EqualTo("{ \"model\": \"opus\" }"));
        });
    }

    [Test]
    public async Task A_write_makes_a_dated_copy_of_the_file_as_it_was()
    {
        Directory.CreateDirectory(home);
        await File.WriteAllTextAsync(path, "before");

        var copy = await file.WriteAsync("after");

        Assert.Multiple(() =>
        {
            Assert.That(copy, Is.EqualTo(path + ".jarvis-20261121-140509.bak"));
            Assert.That(File.ReadAllText(copy!), Is.EqualTo("before"));
            Assert.That(File.ReadAllText(path), Is.EqualTo("after"));
            Assert.That(File.ReadAllBytes(path)[0], Is.EqualTo((byte)'a'), "No byte order mark: Claude Code reads plain UTF-8.");
            Assert.That(Directory.GetFiles(home), Has.Length.EqualTo(2), "The file and its copy, no leftover.");
        });
    }

    [Test]
    public async Task Two_writes_in_the_same_second_keep_both_copies()
    {
        Directory.CreateDirectory(home);
        await File.WriteAllTextAsync(path, "first");

        var one = await file.WriteAsync("second");
        var two = await file.WriteAsync("third");

        Assert.Multiple(() =>
        {
            Assert.That(two, Is.EqualTo(path + ".jarvis-20261121-140509-2.bak"));
            Assert.That(File.ReadAllText(one!), Is.EqualTo("first"));
            Assert.That(File.ReadAllText(two!), Is.EqualTo("second"));
            Assert.That(File.ReadAllText(path), Is.EqualTo("third"));
        });
    }

    [Test]
    public async Task A_write_without_a_file_creates_it_and_makes_no_copy()
    {
        var copy = await file.WriteAsync("{}");

        Assert.Multiple(() =>
        {
            Assert.That(copy, Is.Null);
            Assert.That(File.ReadAllText(path), Is.EqualTo("{}"));
            Assert.That(Directory.GetFiles(home), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task A_cancelled_write_touches_nothing()
    {
        Directory.CreateDirectory(home);
        await File.WriteAllTextAsync(path, "before");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        Assert.That(async () => await file.WriteAsync("after", cancelled.Token), Throws.InstanceOf<OperationCanceledException>());
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(path), Is.EqualTo("before"));
            Assert.That(Directory.GetFiles(home), Has.Length.EqualTo(1));
        });
    }
}
