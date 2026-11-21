using Kazo.Jarvis.Adapters.ClaudeCode;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.LocalServer;

namespace Kazo.Jarvis.Tests.Unit.Adapters.ClaudeCode;

/// <summary>
/// The launcher of Claude Code (ADR 0021, section 1) against a script that stands for <c>claude</c>: the files of
/// Jarvis are written in its own folder, the task and the token reach the process by its environment only, the
/// message goes in, the events come out in order, and a session can be stopped.
/// </summary>
[TestFixture]
public sealed class ClaudeCodeProcessTests
{
    private const string Token = "token-of-the-run";

    private TemporaryFolder folder = null!;
    private string work = null!;
    private string seen = null!;
    private FakeServer server = null!;

    [SetUp]
    public void SetUp()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("The stand-in for claude is a shell script.");
        }

        folder = new TemporaryFolder();
        work = Path.Combine(folder.Path, "work");
        Directory.CreateDirectory(work);
        seen = Path.Combine(folder.Path, "seen.txt");
        server = new FakeServer();
    }

    [TearDown]
    public void TearDown() => folder?.Dispose();

    [Test]
    public async Task The_version_is_read()
    {
        var claude = Launcher(Script("exit 0"));

        var found = await claude.FindAsync();

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.EqualTo(new ClaudeCodeInstall(true, new Version(2, 1, 328))));
            Assert.That(found.UpToDate, Is.True);
            Assert.That(new ClaudeCodeInstall(true, new Version(2, 1, 100)).UpToDate, Is.False);
        });
    }

    [Test]
    public async Task A_program_that_is_not_there_is_not_found_and_not_started()
    {
        var claude = new ClaudeCodeProcess(folder.Paths, server, null, Path.Combine(folder.Path, "nowhere"));

        var found = await claude.FindAsync();

        Assert.Multiple(() =>
        {
            Assert.That(found.Found, Is.False);
            Assert.That(async () => await claude.StartAsync(new ClaudeLaunch("task-1", work, "x")), Throws.InstanceOf<ClaudeCodeException>());
        });
    }

    [Test]
    public void Without_the_local_server_no_session_is_started()
    {
        server.Listening = false;
        var claude = Launcher(Script("exit 0"));

        Assert.That(async () => await claude.StartAsync(new ClaudeLaunch("task-1", work, "x")), Throws.InstanceOf<ClaudeCodeException>());
        Assert.That(File.Exists(seen), Is.False);
    }

    [Test]
    public async Task A_session_receives_its_task_its_token_its_message_and_gives_its_events_in_order()
    {
        File.WriteAllText(Path.Combine(work, "CLAUDE.md"), "Mot secret : orange.");
        var claude = Launcher(Script("""
            read -r message
            {
              echo "cwd=$(pwd)"
              echo "task=$RELAIS_TASK_ID"
              echo "token=$JARVIS_LOCAL_TOKEN"
              echo "args=$*"
              echo "message=$message"
            } > "$SEEN"
            echo '{"type":"system","subtype":"init","session_id":"s-1","model":"haiku"}'
            echo 'not json at all'
            echo '{"type":"assistant","message":{"content":[{"type":"tool_use","id":"toolu_1","name":"Write","input":{"file_path":"a.txt"}}]}}'
            echo '{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"toolu_1"}]}}'
            echo '{"type":"result","subtype":"success","result":"Fait.","total_cost_usd":0.01,"permission_denials":[]}'
            echo "a warning" >&2
            exit 0
            """));

        await using var run = await claude.StartAsync(new ClaudeLaunch("task-1", work, "Corrige \"le menu\"") { BudgetUsd = 2m });
        var events = new List<ClaudeRunEvent>();
        await foreach (var happened in run.ReadAsync())
        {
            events.Add(happened);
        }

        var told = File.ReadAllLines(seen).ToDictionary(line => line[..line.IndexOf('=')], line => line[(line.IndexOf('=') + 1)..]);
        Assert.Multiple(() =>
        {
            Assert.That(events.Select(happened => happened.GetType()), Is.EqualTo(new[] { typeof(ClaudeStarted), typeof(ClaudeToolUse), typeof(ClaudeToolResult), typeof(ClaudeTurnEnded), typeof(ClaudeExited) }));
            Assert.That(events[0], Is.EqualTo(new ClaudeStarted("s-1", "haiku")));
            Assert.That(events[3], Is.EqualTo(new ClaudeTurnEnded("Fait.", 0.01m, false, 0)));
            Assert.That(events[4], Is.EqualTo(new ClaudeExited(0, "a warning")));
            Assert.That(Path.GetFullPath(told["cwd"]), Is.EqualTo(Path.GetFullPath(work)).Or.EndsWith(work));
            Assert.That(told["task"], Is.EqualTo("task-1"));
            Assert.That(told["token"], Is.EqualTo(Token));
            Assert.That(told["args"], Does.Contain("--permission-prompt-tool mcp__jarvis__approve"));
            Assert.That(told["args"], Does.Contain("--append-system-prompt-file " + Path.Combine(work, "CLAUDE.md")));
            Assert.That(told["args"], Does.Contain("--max-budget-usd 2.00"));
            Assert.That(told["args"], Does.Not.Contain(Token), "The token is never on the command line.");
            Assert.That(System.Text.Json.JsonDocument.Parse(told["message"]).RootElement.GetProperty("message").GetProperty("content").GetString(), Is.EqualTo("Corrige \"le menu\""));
        });
    }

    [Test]
    public async Task The_files_of_Jarvis_are_written_in_its_own_folder_and_hold_no_token()
    {
        var claude = Launcher(Script("exit 0"));

        await using var run = await claude.StartAsync(new ClaudeLaunch("task-1", work, "x"));
        await foreach (var unused in run.ReadAsync())
        {
        }

        var settings = File.ReadAllText(claude.SettingsFile);
        var mcp = File.ReadAllText(claude.McpFile);
        Assert.Multiple(() =>
        {
            Assert.That(Path.GetDirectoryName(claude.SettingsFile), Is.EqualTo(Path.Combine(folder.Paths.Root, "claude")));
            Assert.That(settings, Does.Contain("http://127.0.0.1:47999/hook/task.start"));
            Assert.That(mcp, Does.Contain("http://127.0.0.1:47999/mcp"));
            Assert.That(settings + mcp, Does.Not.Contain(Token));
            Assert.That(Directory.Exists(Path.Combine(folder.Path, ".claude")), Is.False, "Nothing is written in the user's Claude Code folder.");
        });
    }

    [Test]
    public async Task A_session_that_fails_tells_its_code_and_the_end_of_its_error_output()
    {
        var claude = Launcher(Script("""
            echo "Error: not logged in" >&2
            exit 3
            """));

        await using var run = await claude.StartAsync(new ClaudeLaunch("task-1", work, "x"));
        var events = new List<ClaudeRunEvent>();
        await foreach (var happened in run.ReadAsync())
        {
            events.Add(happened);
        }

        Assert.That(events, Is.EqualTo(new ClaudeRunEvent[] { new ClaudeExited(3, "Error: not logged in") }));
    }

    [Test]
    public async Task A_session_can_be_stopped()
    {
        var claude = Launcher(Script("""
            echo '{"type":"system","subtype":"init","session_id":"s-1"}'
            sleep 60
            """));

        await using var run = await claude.StartAsync(new ClaudeLaunch("task-1", work, "x"));
        var events = new List<ClaudeRunEvent>();
        await foreach (var happened in run.ReadAsync())
        {
            events.Add(happened);
            if (happened is ClaudeStarted)
            {
                await run.StopAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(events[0], Is.InstanceOf<ClaudeStarted>());
            Assert.That(events[^1], Is.InstanceOf<ClaudeExited>());
            Assert.That(((ClaudeExited)events[^1]).ExitCode, Is.Not.Zero);
        });
    }

    [Test]
    public void A_folder_that_does_not_exist_starts_nothing()
    {
        var claude = Launcher(Script("exit 0"));

        Assert.That(async () => await claude.StartAsync(new ClaudeLaunch("task-1", Path.Combine(folder.Path, "gone"), "x")), Throws.InstanceOf<ClaudeCodeException>());
    }

    private ClaudeCodeProcess Launcher(string script) => new(folder.Paths, server, null, script);

    /// <summary>A script that answers <c>--version</c> like Claude Code, and otherwise runs <paramref name="body"/>.</summary>
    private string Script(string body)
    {
        var path = Path.Combine(folder.Path, "claude");
        File.WriteAllText(path, $"#!/bin/sh\nSEEN='{seen}'\nif [ \"$1\" = \"--version\" ]; then echo '2.1.328 (Claude Code)'; exit 0; fi\n{body.Replace("\r\n", "\n")}\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }

    private sealed class FakeServer : ILocalServer
    {
        public event Action? Changed
        {
            add { }
            remove { }
        }

        public bool Listening { get; set; } = true;

        public LocalServerStatus Status => new(Listening, 47999);

        public string SessionToken => Token;
    }
}
