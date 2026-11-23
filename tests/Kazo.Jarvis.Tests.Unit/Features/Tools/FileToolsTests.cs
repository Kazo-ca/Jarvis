using System.Text.Json;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Tools.Files;
using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Tests.Unit.Adapters;

namespace Kazo.Jarvis.Tests.Unit.Features.Tools;

/// <summary>
/// The file tools (ADR 0017, section 3; ADR 0009, section 5), on a real folder: they read inside the allowed folders
/// only, a path is compared after it is resolved, and what is outside is of class « Interdite ».
/// </summary>
[TestFixture]
public sealed class FileToolsTests
{
    private TemporaryFolder folder = null!;
    private ISettingsStore settings = null!;
    private LocalFiles files = null!;
    private AllowedFolders allowed = null!;
    private ListDirectoryTool list = null!;
    private ReadFileTool read = null!;
    private string notes = null!;
    private string outside = null!;

    [SetUp]
    public async Task SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        files = new LocalFiles();
        allowed = new AllowedFolders(settings, files);
        list = new ListDirectoryTool(allowed, files);
        read = new ReadFileTool(allowed, files);

        notes = Path.Combine(folder.Path, "Notes");
        outside = Path.Combine(folder.Path, "NotesPrivate");
        Directory.CreateDirectory(Path.Combine(notes, "recettes"));
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(notes, "courses.txt"), "farine, œufs, lait");
        File.WriteAllText(Path.Combine(notes, "recettes", "crepes.txt"), "250 g de farine");
        File.WriteAllText(Path.Combine(outside, "secret.txt"), "never read");
        await AllowAsync(notes);
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    private static JsonElement Arguments(object value) => JsonSerializer.SerializeToElement(value);

    private async Task AllowAsync(params string[] folders) =>
        Assert.That((await settings.WriteAsync(FileToolsOptions.Section, settings.Read(FileToolsOptions.Section) with { Folders = folders })).IsSaved, Is.True);

    [Test]
    public void Both_tools_are_reads_enabled_by_default_and_described_in_both_languages()
    {
        foreach (ITool tool in new ITool[] { list, read })
        {
            Assert.Multiple(() =>
            {
                Assert.That(tool.Name, Does.Match("^[a-z][a-z0-9_]*$"));
                Assert.That(tool.Origin, Is.EqualTo(ToolOrigins.BuiltIn));
                Assert.That(tool.DeclaredClass, Is.EqualTo(ActionClass.Read));
                Assert.That(tool.EnabledByDefault, Is.True, "Without an allowed folder it sees nothing.");
                Assert.That(tool.Discloses(AssistantLanguage.French), Is.Null, "Nothing leaves the machine.");
                Assert.That(tool.Describe(AssistantLanguage.French), Is.Not.Empty.And.Not.EqualTo(tool.Describe(AssistantLanguage.English)));
            });
        }
    }

    [Test]
    public void A_path_inside_an_allowed_folder_is_a_read()
    {
        Assert.Multiple(() =>
        {
            Assert.That(read.Classify(Arguments(new { path = Path.Combine(notes, "courses.txt") })), Is.EqualTo(ActionClass.Read));
            Assert.That(read.Classify(Arguments(new { path = Path.Combine(notes, "recettes", "crepes.txt") })), Is.EqualTo(ActionClass.Read));
            Assert.That(list.Classify(Arguments(new { path = notes })), Is.EqualTo(ActionClass.Read));
            Assert.That(list.Classify(Arguments(new { path = notes + Path.DirectorySeparatorChar })), Is.EqualTo(ActionClass.Read));
            Assert.That(list.Classify(Arguments(new { })), Is.EqualTo(ActionClass.Read), "Without a path it lists the allowed folders.");
        });
    }

    [Test]
    public void A_path_outside_the_allowed_folders_is_forbidden()
    {
        Assert.Multiple(() =>
        {
            Assert.That(read.Classify(Arguments(new { path = Path.Combine(outside, "secret.txt") })), Is.EqualTo(ActionClass.Forbidden),
                "« NotesPrivate » starts like « Notes » but is another folder.");
            Assert.That(read.Classify(Arguments(new { path = Path.Combine(notes, "..", "NotesPrivate", "secret.txt") })), Is.EqualTo(ActionClass.Forbidden));
            Assert.That(list.Classify(Arguments(new { path = folder.Path })), Is.EqualTo(ActionClass.Forbidden), "The parent of an allowed folder is not allowed.");
            Assert.That(read.Classify(Arguments(new { path = "courses.txt" })), Is.EqualTo(ActionClass.Forbidden), "A relative path is not usable.");
            Assert.That(read.Classify(Arguments(new { })), Is.EqualTo(ActionClass.Forbidden));
        });
    }

    [Test]
    public void The_comparison_ignores_case()
    {
        Assert.That(AllowedFolders.IsInside("/Home/Demo/Notes", "/home/demo/notes/a.txt"), Is.True);
        Assert.That(AllowedFolders.IsInside("/home/demo/Notes", "/home/demo/NotesPrivate/a.txt"), Is.False);
        Assert.That(AllowedFolders.IsInside("/", "/anything"), Is.True, "A root that ends with a separator holds everything under it.");
    }

    [Test]
    public void A_link_that_points_outside_an_allowed_folder_is_forbidden()
    {
        var link = Path.Combine(notes, "raccourci");
        try
        {
            Directory.CreateSymbolicLink(link, outside);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Ignore("This account cannot create a symbolic link.");
        }

        Assert.Multiple(() =>
        {
            Assert.That(read.Classify(Arguments(new { path = Path.Combine(link, "secret.txt") })), Is.EqualTo(ActionClass.Forbidden));
            Assert.That(list.Classify(Arguments(new { path = link })), Is.EqualTo(ActionClass.Forbidden));
        });
    }

    [Test]
    public async Task A_link_that_points_inside_an_allowed_folder_is_read_at_its_real_place()
    {
        var link = Path.Combine(outside, "vers-notes");
        try
        {
            Directory.CreateSymbolicLink(link, notes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Ignore("This account cannot create a symbolic link.");
        }

        var arguments = Arguments(new { path = Path.Combine(link, "courses.txt") });
        Assert.That(read.Classify(arguments), Is.EqualTo(ActionClass.Read));
        using var result = JsonDocument.Parse(await read.InvokeAsync(arguments, CancellationToken.None));
        Assert.That(result.RootElement.GetProperty("text").GetString(), Is.EqualTo("farine, œufs, lait"));
    }

    [TestCase(".claude/sessions/abc.key", true)]
    [TestCase(".claude/ide/1234.lock", true)]
    [TestCase(".claude/sessions/abc.json", false)]
    [TestCase(".claude/settings.json", false)]
    [TestCase("sessions/abc.key", false)]
    public async Task The_session_keys_and_ide_locks_of_Claude_Code_are_forbidden_everywhere(string relative, bool secret)
    {
        var path = Path.Combine([notes, .. relative.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "x");

        Assert.That(read.Classify(Arguments(new { path })), Is.EqualTo(secret ? ActionClass.Forbidden : ActionClass.Read));
    }

    [Test]
    public async Task Without_an_allowed_folder_everything_is_forbidden()
    {
        await AllowAsync();

        Assert.Multiple(() =>
        {
            Assert.That(read.Classify(Arguments(new { path = Path.Combine(notes, "courses.txt") })), Is.EqualTo(ActionClass.Forbidden));
            Assert.That(list.Classify(Arguments(new { path = notes })), Is.EqualTo(ActionClass.Forbidden));
        });
    }

    [Test]
    public async Task Listing_without_a_path_gives_the_allowed_folders()
    {
        using var result = JsonDocument.Parse(await list.InvokeAsync(Arguments(new { }), CancellationToken.None));

        Assert.That(result.RootElement.GetProperty("allowed_folders").EnumerateArray().Select(item => item.GetString()),
            Is.EqualTo(new[] { files.Resolve(notes) }));
    }

    [Test]
    public async Task Listing_a_folder_gives_its_folders_then_its_files()
    {
        using var result = JsonDocument.Parse(await list.InvokeAsync(Arguments(new { path = notes }), CancellationToken.None));

        var root = result.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("total").GetInt32(), Is.EqualTo(2));
            Assert.That(root.GetProperty("folders").EnumerateArray().Select(item => item.GetString()), Is.EqualTo(new[] { "recettes" }));
            Assert.That(root.GetProperty("files")[0].GetProperty("name").GetString(), Is.EqualTo("courses.txt"));
            Assert.That(root.GetProperty("files")[0].GetProperty("bytes").GetInt64(), Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task A_large_folder_is_cut_so_that_the_result_stays_readable()
    {
        for (var index = 0; index < 150; index++)
        {
            await File.WriteAllTextAsync(Path.Combine(notes, $"un-fichier-au-nom-assez-long-{index:000}.txt"), "x");
        }

        var text = await list.InvokeAsync(Arguments(new { path = notes }), CancellationToken.None);
        using var result = JsonDocument.Parse(text);

        Assert.Multiple(() =>
        {
            Assert.That(text, Has.Length.LessThanOrEqualTo(FileToolsOptions.MostResultCharacters));
            Assert.That(result.RootElement.GetProperty("total").GetInt32(), Is.EqualTo(152));
            Assert.That(result.RootElement.GetProperty("shown").GetInt32(), Is.LessThan(152).And.GreaterThan(5));
        });
    }

    [Test]
    public async Task A_long_file_is_read_by_parts()
    {
        var path = Path.Combine(notes, "long.txt");
        var content = string.Concat(Enumerable.Range(1, 500).Select(line => $"Ligne {line} : « texte ».\n"));
        await File.WriteAllTextAsync(path, content);

        var first = await read.InvokeAsync(Arguments(new { path }), CancellationToken.None);
        using var firstPart = JsonDocument.Parse(first);
        var next = firstPart.RootElement.GetProperty("next_start").GetInt32();
        using var secondPart = JsonDocument.Parse(await read.InvokeAsync(Arguments(new { path, start = next }), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(first, Has.Length.LessThanOrEqualTo(FileToolsOptions.MostResultCharacters), "The tool loop would cut it in the middle of the JSON.");
            Assert.That(firstPart.RootElement.GetProperty("total_characters").GetInt32(), Is.EqualTo(content.Length));
            Assert.That(firstPart.RootElement.GetProperty("text").GetString(), Is.EqualTo(content[..next]));
            Assert.That(secondPart.RootElement.GetProperty("start").GetInt32(), Is.EqualTo(next));
            Assert.That(secondPart.RootElement.GetProperty("text").GetString(), Does.StartWith(content.Substring(next, 20)));
        });
    }

    [Test]
    public async Task The_last_part_of_a_file_has_no_next_start()
    {
        using var result = JsonDocument.Parse(await read.InvokeAsync(Arguments(new { path = Path.Combine(notes, "courses.txt") }), CancellationToken.None));

        Assert.That(result.RootElement.TryGetProperty("next_start", out _), Is.False);
    }

    [Test]
    public void A_call_outside_the_allowed_folders_reads_nothing_even_if_it_runs()
    {
        // The decision point denies it first; the tool checks again all the same.
        Assert.That(async () => await read.InvokeAsync(Arguments(new { path = Path.Combine(outside, "secret.txt") }), CancellationToken.None),
            Throws.TypeOf<ToolException>().With.Message.Contains("outside"));
        Assert.That(async () => await list.InvokeAsync(Arguments(new { path = outside }), CancellationToken.None), Throws.TypeOf<ToolException>());
    }

    [Test]
    public void A_failure_is_one_sentence_for_the_model()
    {
        Assert.Multiple(() =>
        {
            Assert.That(async () => await read.InvokeAsync(Arguments(new { path = Path.Combine(notes, "absent.txt") }), CancellationToken.None),
                Throws.TypeOf<ToolException>().With.Message.EqualTo("Nothing exists at this path."));
            Assert.That(async () => await read.InvokeAsync(Arguments(new { path = Path.Combine(notes, "recettes") }), CancellationToken.None),
                Throws.TypeOf<ToolException>().With.Message.Contains("list_directory"));
            Assert.That(async () => await list.InvokeAsync(Arguments(new { path = Path.Combine(notes, "courses.txt") }), CancellationToken.None),
                Throws.TypeOf<ToolException>().With.Message.Contains("read_file"));
        });
    }

    [Test]
    public void A_file_that_is_not_text_is_refused()
    {
        var path = Path.Combine(notes, "image.bin");
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x00, 0x01, 0x02]);

        Assert.That(async () => await read.InvokeAsync(Arguments(new { path }), CancellationToken.None),
            Throws.TypeOf<ToolException>().With.Message.EqualTo("This file is not a text file."));
    }

    [TestCase(100)]
    [TestCase(5000)]
    public async Task The_size_of_a_result_is_bounded(int size)
    {
        var result = await settings.WriteAsync(FileToolsOptions.Section, settings.Read(FileToolsOptions.Section) with { ResultCharacters = size });

        Assert.That(result.Outcome, Is.EqualTo(SettingsWriteOutcome.Invalid));
    }
}
