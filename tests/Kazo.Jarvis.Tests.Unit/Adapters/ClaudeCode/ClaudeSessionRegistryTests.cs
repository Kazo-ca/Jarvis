using Kazo.Jarvis.Adapters.ClaudeCode;
using Kazo.Jarvis.Kernel.ClaudeCode;

namespace Kazo.Jarvis.Tests.Unit.Adapters.ClaudeCode;

/// <summary>
/// The reader of the registry of the Claude Code sessions (spikes E2 and E3; ADR 0021, section 6): the sessions
/// whose process is alive, with their state, their folder and the Git remote and branch of that folder; a format
/// that is internal to another program, so every missing field is tolerated; and <b>never</b> a read of
/// <c>sessions/*.key</c> or <c>ide/*.lock</c>, which hold secrets.
/// </summary>
[TestFixture]
public sealed class ClaudeSessionRegistryTests
{
    private TemporaryFolder folder = null!;
    private string home = null!;
    private string sessions = null!;
    private List<string> opened = null!;
    private HashSet<int> dead = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        home = Path.Combine(folder.Path, ".claude");
        sessions = Path.Combine(home, "sessions");
        Directory.CreateDirectory(sessions);
        opened = [];
        dead = [];
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public async Task Without_a_registry_there_is_nothing_and_it_is_said()
    {
        Directory.Delete(sessions);

        var listing = await Registry().ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(listing.Found, Is.False, "Claude Code never ran for this user.");
            Assert.That(listing.Sessions, Is.Empty);
        });
    }

    [Test]
    public async Task A_session_is_read_with_its_state_its_folder_and_its_last_activity()
    {
        var work = Work("kazo-bar");
        Session(4101, $$"""
            {"pid":4101,"procStart":"x","sessionId":"sess-1","cwd":{{Json(work)}},"name":"Relire la PR","nameSource":"user",
             "status":"waiting","waitingFor":"permission to run Bash","statusUpdatedAt":1795251600000,"updatedAt":1795251000000,
             "entrypoint":"claude-desktop","hostSessionId":"local_1","futureField":{"a":1} }
            """);

        var session = (await Registry().ReadAsync()).Sessions.Single();

        Assert.Multiple(() =>
        {
            Assert.That(session.SessionId, Is.EqualTo("sess-1"));
            Assert.That(session.ProcessId, Is.EqualTo(4101));
            Assert.That(session.Folder, Is.EqualTo(work));
            Assert.That(session.Name, Is.EqualTo("Relire la PR"));
            Assert.That(session.State, Is.EqualTo(ClaudeSessionState.Waiting));
            Assert.That(session.WaitingFor, Is.EqualTo("permission to run Bash"));
            Assert.That(session.UpdatedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeMilliseconds(1795251600000)));
            Assert.That(session.FromDesktop, Is.True, "A session of code of Claude Desktop is a Claude Code process like the others (spike E3).");
            Assert.That((session.Remote, session.Branch), Is.EqualTo(((string?)null, (string?)null)));
        });
    }

    [Test]
    public async Task Every_field_but_the_three_that_name_the_session_may_be_missing()
    {
        var work = Work("notes");
        Session(1, $$"""{"pid":1,"sessionId":"sess-min","cwd":{{Json(work)}},"status":"thinking-hard","statusUpdatedAt":"soon"}""");

        var session = (await Registry().ReadAsync()).Sessions.Single();

        Assert.Multiple(() =>
        {
            Assert.That(session.Name, Is.EqualTo("notes"), "Without a name: the name of the folder.");
            Assert.That(session.State, Is.EqualTo(ClaudeSessionState.Unknown), "A state this version does not know is shown as such, not guessed.");
            Assert.That(session.WaitingFor, Is.Null);
            Assert.That(session.UpdatedAt, Is.Null);
            Assert.That(session.Entrypoint, Is.Null);
            Assert.That(session.FromDesktop, Is.False);
        });
    }

    [Test]
    public async Task A_file_that_is_not_a_session_is_skipped_and_the_others_are_read()
    {
        var work = Work("ok");
        Session(1, $$"""{"pid":1,"sessionId":"good","cwd":{{Json(work)}},"status":"idle"}""");
        Session(2, """{"pid":2,"sessionId":"half","cw""");
        Session(3, """{"pid":"three","sessionId":"bad-pid","cwd":"/x"}""");
        Session(4, """{"pid":4,"cwd":"/x"}""");
        Session(5, """["not","an","object"]""");
        Session(6, string.Empty);

        var listing = await Registry().ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(listing.Found, Is.True);
            Assert.That(listing.Sessions.Select(session => session.SessionId), Is.EqualTo(new[] { "good" }), "Claude Code rewrites these files: a partial read is skipped.");
        });
    }

    [Test]
    public async Task A_session_whose_process_ended_is_not_listed()
    {
        var work = Work("ok");
        Session(1, $$"""{"pid":1,"sessionId":"alive","cwd":{{Json(work)}},"status":"busy"}""");
        Session(2, $$"""{"pid":2,"sessionId":"gone","cwd":{{Json(work)}},"status":"busy"}""");
        dead.Add(2);

        Assert.That((await Registry().ReadAsync()).Sessions.Select(session => session.SessionId), Is.EqualTo(new[] { "alive" }));
    }

    [Test]
    public async Task The_files_that_hold_secrets_are_never_opened()
    {
        var work = Work("kazo-bar");
        Git(work, "https://github.com/Kazo-ca/bar.git", "feature/tray-menu");
        Session(4101, $$"""{"pid":4101,"sessionId":"sess-1","cwd":{{Json(work)}},"status":"idle"}""");
        File.WriteAllText(Path.Combine(sessions, "4101.key"), "SECRET-SESSION-KEY");
        File.WriteAllText(Path.Combine(sessions, "4101.json.key"), "SECRET-SESSION-KEY");
        File.WriteAllText(Path.Combine(sessions, "notes.jsonx"), """{"pid":9,"sessionId":"x","cwd":"/x"}""");
        Directory.CreateDirectory(Path.Combine(sessions, "nested"));
        File.WriteAllText(Path.Combine(sessions, "nested", "9.json"), """{"pid":9,"sessionId":"nested","cwd":"/x"}""");
        Directory.CreateDirectory(Path.Combine(home, "ide"));
        File.WriteAllText(Path.Combine(home, "ide", "12345.lock"), """{"authToken":"SECRET-IDE-TOKEN"}""");
        File.WriteAllText(Path.Combine(home, ".credentials.json"), """{"token":"SECRET-OAUTH"}""");
        File.WriteAllText(Path.Combine(home, "settings.json"), "{}");

        var listing = await Registry().ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(listing.Sessions.Select(session => session.SessionId), Is.EqualTo(new[] { "sess-1" }));
            Assert.That(opened, Is.EquivalentTo(new[]
            {
                Path.Combine(sessions, "4101.json"),
                Path.Combine(work, ".git", "HEAD"),
                Path.Combine(work, ".git", "config"),
            }), "Only the registry files and the Git files of the folder of a session are opened.");
            Assert.That(opened.Where(file => file.EndsWith(".key", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)), Is.Empty);
            Assert.That(opened.Where(file => file.Contains(Path.Combine(home, "ide"), StringComparison.OrdinalIgnoreCase)), Is.Empty);
        });
    }

    [TestCase("4101.json", true)]
    [TestCase("4101.key", false)]
    [TestCase("4101.json.key", false)]
    [TestCase("4101.lock", false)]
    [TestCase("4101.jsonx", false)]
    public void Only_a_json_file_directly_in_sessions_is_a_registry_file(string name, bool expected) =>
        Assert.Multiple(() =>
        {
            Assert.That(ClaudeSessionRegistry.IsRegistryFile(Path.Combine(sessions, name)), Is.EqualTo(expected));
            Assert.That(ClaudeSessionRegistry.IsRegistryFile(Path.Combine(home, "ide", name)), Is.False, "Never anything of the ide folder.");
        });

    [Test]
    public async Task The_remote_and_the_branch_come_from_the_git_folder_of_the_working_folder_or_of_a_parent()
    {
        var root = Work("kazo-bar");
        Git(root, "git@github.com:Kazo-ca/Bar.git", "feature/tray-menu");
        var inside = Path.Combine(root, "src", "Tray");
        Directory.CreateDirectory(inside);
        Session(1, $$"""{"pid":1,"sessionId":"root","cwd":{{Json(root)}}}""");
        Session(2, $$"""{"pid":2,"sessionId":"inside","cwd":{{Json(inside)}}}""");

        var listing = await Registry().ReadAsync();

        Assert.That(listing.Sessions.OrderBy(session => session.ProcessId).Select(session => (session.Remote, session.Branch)), Is.EqualTo(new[]
        {
            ((string?)"git@github.com:Kazo-ca/Bar.git", (string?)"feature/tray-menu"),
            ("git@github.com:Kazo-ca/Bar.git", "feature/tray-menu"),
        }));
    }

    [Test]
    public async Task A_detached_head_has_no_branch_and_a_worktree_finds_the_remote_of_its_repository()
    {
        var main = Work("jarvis");
        Git(main, "https://github.com/Kazo-ca/Jarvis.git", "main");
        File.WriteAllText(Path.Combine(main, ".git", "HEAD"), "4f1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c\n");
        var tree = Work("jarvis-wt");
        var treeGit = Path.Combine(main, ".git", "worktrees", "wt");
        Directory.CreateDirectory(treeGit);
        File.WriteAllText(Path.Combine(treeGit, "HEAD"), "ref: refs/heads/stage2/11-tasks-and-sessions\n");
        File.WriteAllText(Path.Combine(treeGit, "commondir"), "../..\n");
        File.WriteAllText(Path.Combine(tree, ".git"), $"gitdir: {treeGit}\n");
        Session(1, $$"""{"pid":1,"sessionId":"detached","cwd":{{Json(main)}}}""");
        Session(2, $$"""{"pid":2,"sessionId":"worktree","cwd":{{Json(tree)}}}""");

        var listing = await Registry().ReadAsync();

        Assert.That(listing.Sessions.OrderBy(session => session.ProcessId).Select(session => (session.Remote, session.Branch)), Is.EqualTo(new[]
        {
            ((string?)"https://github.com/Kazo-ca/Jarvis.git", (string?)null),
            ("https://github.com/Kazo-ca/Jarvis.git", "stage2/11-tasks-and-sessions"),
        }));
    }

    [Test]
    public async Task A_working_folder_that_is_gone_does_not_hide_the_session()
    {
        Session(1, """{"pid":1,"sessionId":"lost","cwd":"/no/such/folder/anywhere"}""");

        var session = (await Registry().ReadAsync()).Sessions.Single();

        Assert.That((session.Name, session.Remote, session.Branch), Is.EqualTo(("anywhere", (string?)null, (string?)null)));
    }

    [TestCase("[core]\n\tbare = false\n[remote \"origin\"]\n\turl = https://example.com/a/b.git\n\tfetch = +refs\n", "https://example.com/a/b.git")]
    [TestCase("[remote \"upstream\"]\n\turl = https://example.com/up.git\n[remote \"origin\"]\n  url=git@example.com:a/b\n", "git@example.com:a/b")]
    [TestCase("[remote \"upstream\"]\n\turl = https://example.com/up.git\n", null)]
    [TestCase("[remote \"origin\"]\n\tfetch = +refs\n[branch \"main\"]\n\turl = wrong\n", null)]
    [TestCase("", null)]
    public void The_remote_is_the_url_of_origin(string config, string? expected) =>
        Assert.That(ClaudeSessionRegistry.Origin(config), Is.EqualTo(expected));

    private ClaudeSessionRegistry Registry() => new(
        home,
        pid => !dead.Contains(pid),
        file =>
        {
            opened.Add(file);
            return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        });

    private void Session(int pid, string json) => File.WriteAllText(Path.Combine(sessions, pid + ".json"), json);

    private string Work(string name)
    {
        var path = Path.Combine(folder.Path, "work", name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Git(string work, string remote, string branch)
    {
        var git = Path.Combine(work, ".git");
        Directory.CreateDirectory(git);
        File.WriteAllText(Path.Combine(git, "HEAD"), $"ref: refs/heads/{branch}\n");
        File.WriteAllText(Path.Combine(git, "config"), $"[core]\n\trepositoryformatversion = 0\n[remote \"origin\"]\n\turl = {remote}\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n");
    }

    private static string Json(string text) => System.Text.Json.JsonSerializer.Serialize(text);
}
