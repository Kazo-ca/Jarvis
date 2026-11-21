using System.Text.Json;
using Kazo.Jarvis.Features.ClaudeCode.Launch;
using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Permissions;

namespace Kazo.Jarvis.Tests.Unit.Features.ClaudeCode;

/// <summary>
/// The class of a permission request of a session (ADR 0021, section 4): reads in the folder of the session are
/// class « Lecture », writes and commands « Sensible », and any path outside the folder, or a key or lock file of
/// Claude Code, is forbidden whatever the tool.
/// </summary>
[TestFixture]
public sealed class SessionRulesTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "jarvis-rules", "bar");
    private static readonly string Elsewhere = Path.Combine(Path.GetTempPath(), "jarvis-rules", "notes");
    private readonly FakeFolders folders = new(Root, Elsewhere);

    [TestCase("Read", "file_path")]
    [TestCase("Glob", "path")]
    [TestCase("Grep", "path")]
    public void A_read_in_the_folder_of_the_session_is_a_read(string tool, string field)
    {
        var verdict = Classify(tool, new() { [field] = Path.Combine(Root, "src", "Menu.cs") });

        Assert.That((verdict.Class, verdict.Kind), Is.EqualTo((ActionClass.Read, SessionRequestKind.Read)));
    }

    [Test]
    public void A_search_that_names_no_path_is_a_read_of_the_folder()
    {
        Assert.That(Classify("Grep", new() { ["pattern"] = "TODO" }).Class, Is.EqualTo(ActionClass.Read));
    }

    [TestCase("Write", "file_path")]
    [TestCase("Edit", "file_path")]
    [TestCase("MultiEdit", "file_path")]
    [TestCase("NotebookEdit", "notebook_path")]
    public void A_write_in_the_folder_is_sensitive_and_tells_its_path(string tool, string field)
    {
        var path = Path.Combine(Root, "README.md");

        var verdict = Classify(tool, new() { [field] = path, ["content"] = "x" });

        Assert.That((verdict.Class, verdict.Kind, verdict.Path), Is.EqualTo((ActionClass.Sensitive, SessionRequestKind.Write, path)));
    }

    [Test]
    public void A_relative_path_is_read_from_the_folder_of_the_session()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Classify("Write", new() { ["file_path"] = "docs/notes.md" }).Class, Is.EqualTo(ActionClass.Sensitive));
            Assert.That(Classify("Write", new() { ["file_path"] = "../notes/secret.md" }).Class, Is.EqualTo(ActionClass.Forbidden), "Out of the folder by its dots.");
        });
    }

    [Test]
    public void A_command_is_sensitive_and_tells_the_whole_command()
    {
        var verdict = Classify("Bash", new() { ["command"] = "dotnet test --filter Menu" });

        Assert.That((verdict.Class, verdict.Kind, verdict.Command), Is.EqualTo((ActionClass.Sensitive, SessionRequestKind.Command, "dotnet test --filter Menu")));
    }

    [TestCase("WebFetch")]
    [TestCase("WebSearch")]
    [TestCase("Agent")]
    [TestCase("mcp__github__create_issue")]
    public void Any_other_tool_is_sensitive(string tool)
    {
        Assert.That(Classify(tool, new() { ["url"] = "https://example.com" }).Class, Is.EqualTo(ActionClass.Sensitive));
    }

    [TestCase("Read")]
    [TestCase("Write")]
    [TestCase("Edit")]
    [TestCase("WebFetch")]
    public void A_path_outside_the_folder_of_the_session_is_forbidden_whatever_the_tool(string tool)
    {
        Assert.Multiple(() =>
        {
            Assert.That(Classify(tool, new() { ["file_path"] = Path.Combine(Elsewhere, "journal.md") }).Class, Is.EqualTo(ActionClass.Forbidden),
                "Another allowed folder is not the folder of this session.");
            Assert.That(Classify(tool, new() { ["file_path"] = Path.Combine(Path.GetTempPath(), "jarvis-rules", "outside.txt") }).Class, Is.EqualTo(ActionClass.Forbidden));
            Assert.That(Classify(tool, new() { ["path"] = Root + "-private" }).Class, Is.EqualTo(ActionClass.Forbidden), "A name that starts like the folder is not inside it.");
        });
    }

    [TestCase("sessions", "1234.key")]
    [TestCase("ide", "5678.lock")]
    public void The_key_and_lock_files_of_Claude_Code_are_forbidden_even_inside_the_folder(string folder, string file)
    {
        var path = Path.Combine(Root, ".claude", folder, file);

        Assert.Multiple(() =>
        {
            Assert.That(Classify("Read", new() { ["file_path"] = path }).Class, Is.EqualTo(ActionClass.Forbidden));
            Assert.That(Classify("Write", new() { ["file_path"] = path }).Class, Is.EqualTo(ActionClass.Forbidden));
        });
    }

    [Test]
    public void A_write_that_does_not_say_where_is_forbidden()
    {
        Assert.That(Classify("Write", new() { ["content"] = "x" }).Class, Is.EqualTo(ActionClass.Forbidden));
    }

    [Test]
    public void Nothing_is_allowed_once_the_folder_of_the_session_is_no_longer_an_allowed_one()
    {
        var removed = new FakeFolders(Elsewhere);

        var verdict = SessionRules.Classify("Read", Json(new() { ["file_path"] = Path.Combine(Root, "README.md") }), Root, removed);

        Assert.That(verdict.Class, Is.EqualTo(ActionClass.Forbidden));
    }

    [Test]
    public void Only_the_write_tools_and_Bash_are_watched_in_the_stream()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new[] { "Write", "Edit", "MultiEdit", "NotebookEdit", "Bash" }.All(SessionRules.WritesOrRuns), Is.True);
            Assert.That(new[] { "Read", "Glob", "Grep", "TodoWrite", "WebFetch" }.Any(SessionRules.WritesOrRuns), Is.False);
        });
    }

    private SessionVerdict Classify(string tool, Dictionary<string, string> input) => SessionRules.Classify(tool, Json(input), Root, folders);

    private static JsonElement Json(Dictionary<string, string> input) => JsonSerializer.SerializeToElement(input);
}

/// <summary>Allowed folders in memory: paths are made full, and the key and lock files of Claude Code are secrets.</summary>
internal sealed class FakeFolders(params string[] roots) : IAllowedFolders
{
    public List<string> Folders { get; } = [.. roots];

    public IReadOnlyList<string> Roots => Folders;

    public CheckedPath Check(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            return new CheckedPath(PathVerdict.Unusable, null);
        }

        var full = Path.GetFullPath(path);
        if (!Folders.Any(root => full == root || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
        {
            return new CheckedPath(PathVerdict.Outside, full);
        }

        var secret = full.Contains(Path.DirectorySeparatorChar + ".claude" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && (full.EndsWith(".key", StringComparison.OrdinalIgnoreCase) || full.EndsWith(".lock", StringComparison.OrdinalIgnoreCase));
        return new CheckedPath(secret ? PathVerdict.Secret : PathVerdict.Allowed, full);
    }
}
