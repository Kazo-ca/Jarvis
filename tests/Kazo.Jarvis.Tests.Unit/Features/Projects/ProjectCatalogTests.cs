using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Projects.Catalog;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Projects;
using Kazo.Jarvis.Tests.Unit.Adapters;

namespace Kazo.Jarvis.Tests.Unit.Features.Projects;

/// <summary>
/// The projects (ADR 0025, sections 6, 10 and 11): a name and a local folder kept in the settings, the project a
/// request names in its words, and the block an agent is told, with the memory read from the file of the folder.
/// </summary>
[TestFixture]
public sealed class ProjectCatalogTests
{
    private TemporaryFolder folder = null!;
    private JsonFileSettingsStore settings = null!;
    private FakeLocalFiles files = null!;
    private ProjectCatalog catalog = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        files = new FakeLocalFiles();
        catalog = new ProjectCatalog(settings, files);
    }

    [TearDown]
    public void TearDown()
    {
        catalog.Dispose();
        folder.Dispose();
    }

    [Test]
    public async Task A_project_is_a_name_and_a_folder_and_two_projects_cannot_share_a_name()
    {
        var changes = 0;
        catalog.Changed += () => Interlocked.Increment(ref changes);

        var added = await catalog.AddAsync("  Kazo OS ", "\"C:\\Projets\\KazoOs\"");
        var same = await catalog.AddAsync("kazo os", @"C:\Ailleurs");
        var noName = await catalog.AddAsync(" -- ", @"C:\Projets\X");
        var tooLong = await catalog.AddAsync(new string('a', 61), @"C:\Projets\X");
        var relative = await catalog.AddAsync("Maison", "Projets\\Maison");
        var second = await catalog.AddAsync("Maison", "/home/benoit/maison");

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.EqualTo(ProjectOutcome.Added));
            Assert.That(same, Is.EqualTo(ProjectOutcome.NameTaken), "Whatever the case.");
            Assert.That(noName, Is.EqualTo(ProjectOutcome.BadName));
            Assert.That(tooLong, Is.EqualTo(ProjectOutcome.BadName));
            Assert.That(relative, Is.EqualTo(ProjectOutcome.BadFolder));
            Assert.That(second, Is.EqualTo(ProjectOutcome.Added));
            Assert.That(catalog.Projects, Is.EqualTo(new[]
            {
                new ProjectDefinition("kazo-os", "Kazo OS", @"C:\Projets\KazoOs"),
                new ProjectDefinition("maison", "Maison", "/home/benoit/maison"),
            }));
            Assert.That(changes, Is.EqualTo(2), "Listeners are told at each project created.");
            Assert.That(new ProjectCatalog(settings, files).Projects, Has.Count.EqualTo(2), "Kept in the settings.");
        });

        Assert.That(await catalog.RemoveAsync("kazo-os"), Is.True);
        Assert.That(catalog.Projects.Select(project => project.Id), Is.EqualTo(new[] { "maison" }));
    }

    [Test]
    public async Task A_project_is_found_by_its_name_and_named_by_whole_words_the_longest_first()
    {
        await catalog.AddAsync("Kazo", @"C:\Projets\Kazo");
        await catalog.AddAsync("Kazo OS", @"C:\Projets\KazoOs");
        await catalog.AddAsync("Os", @"C:\Projets\Os");
        await catalog.AddAsync("Rénovation", @"C:\Projets\Reno");

        Assert.Multiple(() =>
        {
            Assert.That(catalog.Find("KAZO os")?.Id, Is.EqualTo("kazo-os"));
            Assert.That(catalog.Find("renovation")?.Id, Is.EqualTo("renovation"), "Whatever the accents.");
            Assert.That(catalog.Find("kazo-os")?.Name, Is.EqualTo("Kazo OS"), "By its identity too.");
            Assert.That(catalog.Find("Jardin"), Is.Null);
            Assert.That(catalog.Find(" "), Is.Null);

            Assert.That(catalog.NamedIn("Dans Kazo Os, il nous reste quoi à faire ?")?.Id, Is.EqualTo("kazo-os"), "The longest name wins.");
            Assert.That(catalog.NamedIn("Où en est Kazo ?")?.Id, Is.EqualTo("kazo"));
            Assert.That(catalog.NamedIn("Ouvre le dossier des photos.")?.Id, Is.Null, "« Os » is not named by « dossier ».");
            Assert.That(catalog.NamedIn("Où en est la rénovation ?")?.Id, Is.EqualTo("renovation"));
            Assert.That(catalog.NamedIn(null), Is.Null);
        });
    }

    [Test]
    public async Task An_agent_is_told_the_folder_and_the_memory_file_of_the_project()
    {
        await catalog.AddAsync("Kazo OS", @"C:\Projets\KazoOs");
        await catalog.AddAsync("Maison", "/home/benoit/maison/");
        files.Texts[@"C:\Projets\KazoOs\JARVIS.md"] = "# Kazo OS\nReste à faire : le pilote [réseau].\n";
        var kazo = catalog.Find("Kazo OS")!;
        var maison = catalog.Find("Maison")!;

        var told = await catalog.RecallAsync(kazo, AssistantLanguage.French, 4096);
        var empty = await catalog.RecallAsync(maison, AssistantLanguage.English, 4096);

        Assert.Multiple(() =>
        {
            Assert.That(told, Does.StartWith("Ce qui suit, entre « [Projet] » et « [Fin du projet] »"));
            Assert.That(told, Does.Contain("\n[Projet]\nNom : Kazo OS\nDossier : C:\\Projets\\KazoOs\nMémoire du projet (fichier JARVIS.md de son dossier) :\n# Kazo OS\nReste à faire : le pilote (réseau).\n[Fin du projet]"),
                "No bracket of the file closes the block.");
            Assert.That(empty, Does.Contain("\n[Project]\nName: Maison\nFolder: /home/benoit/maison/\nThe project has no memory yet (no file JARVIS.md in its folder).\n[End of the project]"));
            Assert.That(ProjectCatalog.MemoryPath(maison), Is.EqualTo("/home/benoit/maison/JARVIS.md"));
        });
    }

    [Test]
    public async Task A_long_memory_file_is_cut_to_the_share_of_the_project()
    {
        await catalog.AddAsync("Kazo OS", "/projets/kazo");
        files.Texts["/projets/kazo/JARVIS.md"] = new string('a', 1000);

        // 64 tokens of context: 12 characters for the project.
        var told = await catalog.RecallAsync(catalog.Find("Kazo OS")!, AssistantLanguage.French, 64);

        Assert.Multiple(() =>
        {
            Assert.That(ProjectCatalog.Budget(64), Is.EqualTo(12));
            Assert.That(told, Does.Contain("\naaaaaaaaaaaa…\n[Fin du projet]"));
        });
    }
}
