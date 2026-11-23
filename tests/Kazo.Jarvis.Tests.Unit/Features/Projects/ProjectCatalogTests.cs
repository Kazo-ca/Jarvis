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

    [Test]
    public async Task A_folder_where_a_session_works_and_that_belongs_to_no_project_waits_until_the_user_says_where_it_belongs()
    {
        var store = new Kazo.Jarvis.Adapters.Storage.SqliteDocumentStore(folder.Paths, TimeProvider.System);
        var board = new Kazo.Jarvis.Features.Tasks.Board.TaskBoard(store, TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<Kazo.Jarvis.Features.Tasks.Board.TaskBoard>.Instance);
        using var followed = new ProjectCatalog(settings, files, board);
        var changes = 0;
        followed.Changed += () => Interlocked.Increment(ref changes);
        await followed.AddAsync("Kazo OS", @"C:\Projets\KazoOs");

        var inside = await board.CreateAsync(Session(@"c:/projets/kazoos/src"));
        var bar = await board.CreateAsync(Session(@"C:\Projets\KazoBar"));
        await board.CreateAsync(Session(@"C:\Projets\KazoBar\"));
        var notes = await board.CreateAsync(Session(@"C:\Notes"));
        await board.CreateAsync(Session(null));
        var waiting = followed.Unassigned;
        var seen = changes;

        // Assigned to a project, or a project created on it: the request is gone. An ended session asks nothing.
        var assigned = await followed.AssignAsync("kazo-os", @"C:\Projets\KazoBar");
        var unknown = await followed.AssignAsync("nobody", @"C:\Notes");
        var afterAssign = followed.Unassigned;
        await board.ChangeAsync(notes.Id, new Kazo.Jarvis.Kernel.Tasks.TaskChange(Kazo.Jarvis.Kernel.Tasks.WorkTaskStatus.Done));
        var afterEnd = followed.Unassigned;
        await followed.UnassignAsync("kazo-os", @"c:\projets\kazobar");

        Assert.Multiple(() =>
        {
            Assert.That(waiting, Is.EquivalentTo(new[] { @"C:\Projets\KazoBar", @"C:\Notes" }), "Once per folder, whatever the slash and the case.");
            Assert.That(seen, Is.GreaterThanOrEqualTo(5), "The page is told when the board moves.");
            Assert.That((assigned, unknown), Is.EqualTo((true, false)));
            Assert.That(afterAssign, Is.EqualTo(new[] { @"C:\Notes" }));
            Assert.That(afterEnd, Is.Empty);
            Assert.That(followed.OwnerOf(inside.Folder)?.Name, Is.EqualTo("Kazo OS"));
            Assert.That(followed.MemoryFileOf(inside.Folder), Is.EqualTo(ProjectCatalog.MemoryPath(followed.OwnerOf(inside.Folder)!)), "A session there is told where the memory is.");
            Assert.That(followed.MemoryFileOf(@"C:\Notes"), Is.Null);
            Assert.That(followed.Unassigned, Is.EqualTo(new[] { @"C:\Projets\KazoBar" }), "Taken back: the request returns.");
            Assert.That(followed.MoreFoldersOf("kazo-os"), Is.Empty);
            Assert.That(catalog.Unassigned, Is.Empty, "Without the board nothing is ever asked.");
            Assert.That(bar.Folder, Is.Not.Null);
        });

        static Kazo.Jarvis.Kernel.Tasks.NewWorkTask Session(string? where) => new(
            "travail", "projet", "sujet", new Kazo.Jarvis.Kernel.Tasks.TaskOperator(Kazo.Jarvis.Kernel.Tasks.TaskOperator.ClaudeCode),
            Kazo.Jarvis.Kernel.Tasks.TaskSource.Hook, where, Status: Kazo.Jarvis.Kernel.Tasks.WorkTaskStatus.Working);
    }

    [Test]
    public async Task A_fact_is_written_as_one_line_at_the_end_of_the_memory_file_and_a_secret_never_is()
    {
        await catalog.AddAsync("Kazo OS", "/projets/kazo-os");
        await catalog.AddAsync("Maison", "/projets/maison");
        var os = catalog.Find("Kazo OS")!;
        var home = catalog.Find("Maison")!;
        files.Texts["/projets/kazo-os/JARVIS.md"] = "# Kazo OS\n- Le pilote réseau reste à faire.";
        var changes = 0;
        catalog.Changed += () => Interlocked.Increment(ref changes);

        var added = await catalog.RememberAsync(os, "  - La cible est\n ARM64. ");
        var again = await catalog.RememberAsync(os, "la cible est arm64.");
        var created = await catalog.RememberAsync(home, "Le plombier vient mardi.");
        var empty = await catalog.RememberAsync(os, " \n ");
        var tooLong = await catalog.RememberAsync(os, new string('a', ProjectCatalog.MostFactCharacters + 1));
        var secret = await catalog.RememberAsync(os, "Le mot de passe du serveur est hunter2");
        files.ReadOnly = true;
        var refused = await catalog.RememberAsync(os, "Un autre fait.");

        Assert.Multiple(() =>
        {
            Assert.That(new[] { added, again, created, empty, tooLong, secret, refused }, Is.EqualTo(new[]
            {
                ProjectFactOutcome.Added, ProjectFactOutcome.AlreadyKnown, ProjectFactOutcome.Added, ProjectFactOutcome.Empty,
                ProjectFactOutcome.TooLong, ProjectFactOutcome.LooksLikeSecret, ProjectFactOutcome.NotWritten,
            }));
            Assert.That(files.Texts["/projets/kazo-os/JARVIS.md"], Is.EqualTo("# Kazo OS\n- Le pilote réseau reste à faire.\n- La cible est ARM64.\n"), "What was there is kept, on its own line.");
            Assert.That(files.Texts["/projets/maison/JARVIS.md"], Is.EqualTo("# Maison\n\n- Le plombier vient mardi.\n"), "A new file starts with the name of the project.");
            Assert.That(changes, Is.EqualTo(2), "The page reads the memory again.");
        });
    }

    [Test]
    public async Task The_tool_asks_a_confirmation_names_what_it_writes_and_refuses_a_project_nobody_has()
    {
        await catalog.AddAsync("Kazo OS", "/projets/kazo-os");
        var tool = new RememberProjectFactTool(catalog);
        var arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { project = "kazo os", text = "La cible est ARM64." });
        var unknown = System.Text.Json.JsonSerializer.SerializeToElement(new { project = "Jardin", text = "x" });

        var result = await tool.InvokeAsync(arguments, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That((tool.DeclaredClass, tool.Classify(arguments)), Is.EqualTo((Kazo.Jarvis.Kernel.Permissions.ActionClass.Sensitive, Kazo.Jarvis.Kernel.Permissions.ActionClass.Sensitive)));
            Assert.That(tool.RefusedAfterOutsideContent, Is.True);
            Assert.That(tool.Summarize(arguments, AssistantLanguage.French), Is.EqualTo("Écrire dans la mémoire du projet Kazo OS : « La cible est ARM64. »"));
            Assert.That(tool.Summarize(unknown, AssistantLanguage.French), Is.Null);
            Assert.That(tool.Schema.GetRawText(), Does.Contain("\"enum\":[\"Kazo OS\"]"));
            Assert.That(result, Is.EqualTo("{\"remembered\":true,\"project\":\"Kazo OS\"}"));
            Assert.That(files.Texts["/projets/kazo-os/JARVIS.md"], Does.EndWith("- La cible est ARM64.\n"));
            Assert.ThrowsAsync<Kazo.Jarvis.Kernel.Tools.ToolException>(async () => await tool.InvokeAsync(unknown, CancellationToken.None));
        });
    }
}
