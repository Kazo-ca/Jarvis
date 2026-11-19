using System.Text;
using Kazo.Jarvis.Adapters.Storage;
using Kazo.Jarvis.Kernel.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Storage;

/// <summary>The SQLite document store, data\jarvis.db (ADR 0004, section 3; stage 1 plan, work item 3).</summary>
[TestFixture]
public sealed class SqliteDocumentStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 20, 0, 0, TimeSpan.Zero);

    private TemporaryFolder folder = null!;
    private FakeTimeProvider time = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        time = new FakeTimeProvider(Start);
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public async Task A_document_is_added_then_read_back_after_a_restart()
    {
        var added = await Open().Collection<Turn>("Conversation.Turns").AddAsync(new Turn("user", "Bonjour Jarvis"));

        var read = await Open().Collection<Turn>("Conversation.Turns").GetAsync(added.Id);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(folder.Paths.DatabaseFile), Is.True);
            Assert.That(read, Is.EqualTo(added));
            Assert.That(read!.At, Is.EqualTo(Start));
        });
    }

    [Test]
    public async Task A_document_belongs_to_its_collection_only()
    {
        var store = Open();
        var added = await store.Collection<Turn>("Conversation.Turns").AddAsync(new Turn("user", "a"));
        var other = store.Collection<Turn>("Memory.Facts");

        Assert.Multiple(async () =>
        {
            Assert.That(await other.GetAsync(added.Id), Is.Null);
            Assert.That(await other.DeleteAsync(added.Id), Is.False);
            Assert.That(await other.ReadPeriodAsync(Start.AddDays(-1), Start.AddDays(1)), Is.Empty);
        });
    }

    [Test]
    public async Task A_period_is_read_oldest_first_start_included_end_excluded()
    {
        var turns = Open().Collection<Turn>("Conversation.Turns");
        await turns.AddAsync(new Turn("user", "before"), Start.AddMinutes(-1));
        await turns.AddAsync(new Turn("user", "third"), Start.AddMinutes(2));
        await turns.AddAsync(new Turn("user", "first"), Start);
        await turns.AddAsync(new Turn("user", "second"), Start.AddMinutes(2).AddMilliseconds(-1));
        await turns.AddAsync(new Turn("user", "fourth"), Start.AddMinutes(2));
        await turns.AddAsync(new Turn("user", "end"), Start.AddMinutes(5));

        var period = await turns.ReadPeriodAsync(Start, Start.AddMinutes(5));

        Assert.That(period.Select(document => document.Content.Text), Is.EqualTo(new[] { "first", "second", "third", "fourth" }));
    }

    [Test]
    public async Task A_document_is_deleted_and_a_collection_emptied()
    {
        var store = Open();
        var turns = store.Collection<Turn>("Conversation.Turns");
        var facts = store.Collection<Turn>("Memory.Facts");
        var first = await turns.AddAsync(new Turn("user", "a"));
        await turns.AddAsync(new Turn("user", "b"));
        await turns.AddAsync(new Turn("user", "c"));
        var fact = await facts.AddAsync(new Turn("system", "kept"));

        var deleted = await turns.DeleteAsync(first.Id);
        var deletedAgain = await turns.DeleteAsync(first.Id);
        var cleared = await turns.ClearAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(deletedAgain, Is.False);
            Assert.That(cleared, Is.EqualTo(2));
            Assert.That(await turns.ReadPeriodAsync(DateTimeOffset.MinValue.AddYears(2000), Start.AddYears(1)), Is.Empty);
            Assert.That(await facts.GetAsync(fact.Id), Is.Not.Null);
        });
    }

    [Test]
    public async Task Erasing_everything_empties_every_collection_and_leaves_nothing_readable_in_the_file()
    {
        const string marker = "TRACE-TO-ERASE-7f3a9";
        var store = Open();
        for (var index = 0; index < 50; index++)
        {
            await store.Collection<Turn>("Conversation.Turns").AddAsync(new Turn("user", $"{marker} {index}"));
            await store.Collection<Turn>("Memory.Facts").AddAsync(new Turn("system", $"{marker} fact {index}"));
        }

        Assert.That(FilesContain(marker), Is.True, "The marker must be in the files before the erasure, or the test proves nothing.");

        await store.EraseAllAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await store.Collection<Turn>("Conversation.Turns").ReadPeriodAsync(Start.AddDays(-1), Start.AddDays(1)), Is.Empty);
            Assert.That(await store.Collection<Turn>("Memory.Facts").ReadPeriodAsync(Start.AddDays(-1), Start.AddDays(1)), Is.Empty);
            Assert.That(FilesContain(marker), Is.False);
        });
    }

    [Test]
    public void An_invalid_collection_name_is_refused() =>
        Assert.That(() => Open().Collection<Turn>("turns"), Throws.ArgumentException);

    [Test]
    public async Task A_database_from_a_newer_version_is_left_untouched()
    {
        await Open().Collection<Turn>("Conversation.Turns").AddAsync(new Turn("user", "a"));
        SetUserVersion(SqliteDocumentStore.SchemaSteps.Count + 1);
        var before = await File.ReadAllBytesAsync(folder.Paths.DatabaseFile);

        var store = Open();

        Assert.Multiple(async () =>
        {
            Assert.That(async () => await store.Collection<Turn>("Conversation.Turns").AddAsync(new Turn("user", "b")),
                Throws.TypeOf<StorageUnavailableException>().With.Message.Contains("newer version"));
            Assert.That(async () => await store.EraseAllAsync(), Throws.TypeOf<StorageUnavailableException>());
            Assert.That(await File.ReadAllBytesAsync(folder.Paths.DatabaseFile), Is.EqualTo(before));
        });
    }

    [Test]
    public async Task An_older_database_is_copied_to_backups_then_migrated()
    {
        var added = await Open().Collection<Turn>("Conversation.Turns").AddAsync(new Turn("user", "kept"));
        var steps = SqliteDocumentStore.SchemaSteps.Append("CREATE TABLE extra (x INTEGER);").ToArray();

        var migrated = new SqliteDocumentStore(folder.Paths.DatabaseFile, folder.Paths.Backups, time, null, steps);
        var read = await migrated.Collection<Turn>("Conversation.Turns").GetAsync(added.Id);

        var copy = Path.Combine(folder.Paths.Backups, "jarvis.v1.20261119-200000.db");
        Assert.Multiple(() =>
        {
            Assert.That(read?.Content, Is.EqualTo(new Turn("user", "kept")));
            Assert.That(UserVersion(folder.Paths.DatabaseFile), Is.EqualTo(2));
            Assert.That(File.Exists(copy), Is.True);
            Assert.That(UserVersion(copy), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task A_failed_migration_makes_the_store_unavailable_at_its_old_version()
    {
        await Open().Collection<Turn>("Conversation.Turns").AddAsync(new Turn("user", "a"));
        var steps = SqliteDocumentStore.SchemaSteps.Append("THIS IS NOT SQL;").ToArray();

        var store = new SqliteDocumentStore(folder.Paths.DatabaseFile, folder.Paths.Backups, time, null, steps);

        Assert.Multiple(() =>
        {
            Assert.That(async () => await store.Collection<Turn>("Conversation.Turns").ClearAsync(),
                Throws.TypeOf<StorageUnavailableException>().With.Message.Contains("schema 1"));
            Assert.That(UserVersion(folder.Paths.DatabaseFile), Is.EqualTo(1));
        });
    }

    private SqliteDocumentStore Open() => new(folder.Paths, time);

    private bool FilesContain(string marker)
    {
        var needle = Encoding.UTF8.GetBytes(marker);
        return Directory.GetFiles(folder.Paths.Data)
            .Any(file => File.ReadAllBytes(file).AsSpan().IndexOf(needle) >= 0);
    }

    private void SetUserVersion(int version)
    {
        using var connection = new SqliteConnection($"Data Source={folder.Paths.DatabaseFile};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA wal_checkpoint(TRUNCATE); PRAGMA user_version = {version};";
        command.ExecuteNonQuery();
    }

    private static long UserVersion(string file)
    {
        using var connection = new SqliteConnection($"Data Source={file};Pooling=False;Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return (long)command.ExecuteScalar()!;
    }

    private sealed record Turn(string Role, string Text);
}
