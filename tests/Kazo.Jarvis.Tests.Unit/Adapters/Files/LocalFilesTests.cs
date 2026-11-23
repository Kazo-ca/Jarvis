using System.Text;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Files;

namespace Kazo.Jarvis.Tests.Unit.Adapters.Files;

/// <summary>The disk read only (ADR 0017, section 3): how a path is resolved and how a file is read as text.</summary>
[TestFixture]
public sealed class LocalFilesTests
{
    private TemporaryFolder folder = null!;
    private LocalFiles files = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        files = new LocalFiles();
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("notes.txt")]
    [TestCase("../notes.txt")]
    public void A_path_that_is_not_absolute_cannot_be_resolved(string path) =>
        Assert.That(files.Resolve(path), Is.Null);

    [Test]
    public void A_path_with_a_zero_character_cannot_be_resolved() =>
        Assert.That(files.Resolve(Path.Combine(folder.Path, "a\0b")), Is.Null);

    [Test]
    public async Task Text_is_added_at_the_end_of_a_file_created_when_missing_without_a_mark()
    {
        var file = Path.Combine(folder.Path, "JARVIS.md");

        await files.AppendTextAsync(file, "# Projet\n");
        await files.AppendTextAsync(file, "- été\n");
        var bytes = await File.ReadAllBytesAsync(file);

        Assert.Multiple(() =>
        {
            Assert.That(System.Text.Encoding.UTF8.GetString(bytes), Is.EqualTo("# Projet\n- été\n"));
            Assert.That(bytes[0], Is.EqualTo((byte)'#'), "No byte order mark.");
            Assert.That(
                Assert.ThrowsAsync<Kazo.Jarvis.Kernel.Files.LocalFileException>(async () => await files.AppendTextAsync(Path.Combine(folder.Path, "missing", "JARVIS.md"), "x"))!.Failure,
                Is.EqualTo(Kazo.Jarvis.Kernel.Files.LocalFileFailure.NotFound));
            Assert.That(
                Assert.ThrowsAsync<Kazo.Jarvis.Kernel.Files.LocalFileException>(async () => await files.AppendTextAsync(folder.Path, "x"))!.Failure,
                Is.EqualTo(Kazo.Jarvis.Kernel.Files.LocalFileFailure.NotAFile));
        });
    }

    [Test]
    public void Dots_are_resolved_and_a_missing_path_is_kept()
    {
        var resolved = files.Resolve(Path.Combine(folder.Path, "a", "..", "b", "missing.txt"));

        Assert.That(resolved, Is.EqualTo(Path.Combine(files.Resolve(folder.Path)!, "b", "missing.txt")));
    }

    [Test]
    public void A_link_is_replaced_by_its_target()
    {
        var target = Directory.CreateDirectory(Path.Combine(folder.Path, "target")).FullName;
        var link = Path.Combine(folder.Path, "link");
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Ignore("This account cannot create a symbolic link.");
        }

        Assert.That(files.Resolve(Path.Combine(link, "file.txt")), Is.EqualTo(Path.Combine(files.Resolve(target)!, "file.txt")));
    }

    [Test]
    public void A_loop_of_links_cannot_be_resolved()
    {
        var first = Path.Combine(folder.Path, "first");
        var second = Path.Combine(folder.Path, "second");
        try
        {
            Directory.CreateSymbolicLink(first, second);
            Directory.CreateSymbolicLink(second, first);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Ignore("This account cannot create a symbolic link.");
        }

        Assert.That(files.Resolve(Path.Combine(first, "file.txt")), Is.Null);
    }

    [Test]
    public async Task A_folder_lists_its_folders_first_then_its_files_by_name()
    {
        Directory.CreateDirectory(Path.Combine(folder.Path, "zeta"));
        Directory.CreateDirectory(Path.Combine(folder.Path, "Alpha"));
        await File.WriteAllTextAsync(Path.Combine(folder.Path, "b.txt"), "12345");
        await File.WriteAllTextAsync(Path.Combine(folder.Path, "A.txt"), "1");

        var listing = await files.ListAsync(folder.Path, 3);

        Assert.Multiple(() =>
        {
            Assert.That(listing.Total, Is.EqualTo(4));
            Assert.That(listing.Entries.Select(entry => entry.Name), Is.EqualTo(new[] { "Alpha", "zeta", "A.txt" }));
            Assert.That(listing.Entries[0].IsFolder, Is.True);
            Assert.That(listing.Entries[2].Size, Is.EqualTo(1));
        });
    }

    [Test]
    public void A_missing_folder_and_a_file_in_place_of_a_folder_are_named()
    {
        File.WriteAllText(Path.Combine(folder.Path, "a.txt"), "x");

        Assert.Multiple(() =>
        {
            Assert.That(async () => await files.ListAsync(Path.Combine(folder.Path, "missing"), 10),
                Throws.TypeOf<LocalFileException>().With.Property(nameof(LocalFileException.Failure)).EqualTo(LocalFileFailure.NotFound));
            Assert.That(async () => await files.ListAsync(Path.Combine(folder.Path, "a.txt"), 10),
                Throws.TypeOf<LocalFileException>().With.Property(nameof(LocalFileException.Failure)).EqualTo(LocalFileFailure.NotAFolder));
            Assert.That(async () => await files.ReadTextAsync(folder.Path, 0, 10),
                Throws.TypeOf<LocalFileException>().With.Property(nameof(LocalFileException.Failure)).EqualTo(LocalFileFailure.NotAFile));
        });
    }

    [Test]
    public async Task A_part_of_a_text_is_read_with_the_length_of_the_whole()
    {
        var path = Path.Combine(folder.Path, "a.txt");
        await File.WriteAllTextAsync(path, "Bonjour à tous", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var excerpt = await files.ReadTextAsync(path, 8, 100);

        Assert.Multiple(() =>
        {
            Assert.That(excerpt.Text, Is.EqualTo("à tous"), "The mark at the start of the file is not a character of the text.");
            Assert.That(excerpt.Start, Is.EqualTo(8));
            Assert.That(excerpt.TotalCharacters, Is.EqualTo(14));
        });
    }

    [Test]
    public void Text_is_decoded_by_its_mark_or_as_UTF8_and_bytes_are_not_text()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LocalFiles.Decode([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("été")]), Is.EqualTo("été"));
            Assert.That(LocalFiles.Decode(Encoding.UTF8.GetBytes("été")), Is.EqualTo("été"));
            Assert.That(LocalFiles.Decode(Encoding.Latin1.GetBytes("été")), Is.EqualTo("été"), "An old file in the Windows code page.");
            Assert.That(LocalFiles.Decode([1, 2, 0, 3]), Is.Null);
            Assert.That(LocalFiles.Decode([]), Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void A_file_too_large_is_refused()
    {
        var path = Path.Combine(folder.Path, "big.txt");
        using (var stream = File.Create(path))
        {
            stream.SetLength(LocalFiles.MostFileBytes + 1);
        }

        Assert.That(async () => await files.ReadTextAsync(path, 0, 10),
            Throws.TypeOf<LocalFileException>().With.Property(nameof(LocalFileException.Failure)).EqualTo(LocalFileFailure.TooLarge));
    }
}
