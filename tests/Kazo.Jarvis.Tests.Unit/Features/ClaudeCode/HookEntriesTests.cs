using System.Text.Json.Nodes;
using Kazo.Jarvis.Features.ClaudeCode.Hooks;

namespace Kazo.Jarvis.Tests.Unit.Features.ClaudeCode;

/// <summary>
/// The lines of Jarvis in the user's Claude Code settings (ADR 0021, section 6): added without touching the
/// others, recognized by their address whatever the port, removed alone, and never written into a file Jarvis
/// does not know how to read.
/// </summary>
[TestFixture]
public sealed class HookEntriesTests
{
    private const string Token = "ABCDEF0123";

    private const string Own = """
        {
          "model": "opus",
          "permissions": { "allow": ["Bash(git status)"] },
          "hooks": {
            "Stop": [
              { "hooks": [ { "type": "command", "command": "echo \"fini\" && curl http://127.0.0.1:9999/other" } ] }
            ],
            "PreToolUse": [
              { "matcher": "Bash", "hooks": [ { "type": "http", "url": "http://localhost:8080/hooks/pre" } ] }
            ]
          }
        }
        """;

    [Test]
    public void The_preview_shows_five_background_commands_with_the_address_and_the_token()
    {
        var preview = JsonNode.Parse(HookEntries.Preview(47821, Token))!["hooks"]!.AsObject();

        Assert.That(preview.Select(pair => pair.Key), Is.EqualTo(new[] { "SessionStart", "UserPromptSubmit", "PermissionRequest", "SubagentStop", "Stop" }));
        var start = preview["UserPromptSubmit"]![0]!["hooks"]![0]!;
        Assert.Multiple(() =>
        {
            Assert.That(start["type"]!.GetValue<string>(), Is.EqualTo("command"));
            Assert.That(start["async"]!.GetValue<bool>(), Is.True, "In the background: a closed Jarvis never makes a session wait.");
            Assert.That(start["command"]!.GetValue<string>(), Is.EqualTo(
                "curl.exe -s -m 2 --noproxy 127.0.0.1 -X POST -H \"Authorization: Bearer ABCDEF0123\" -H \"Content-Type: application/json\" --data-binary @- http://127.0.0.1:47821/hook/task.start"));
            Assert.That(preview["SessionStart"]![0]!["hooks"]![0]!["command"]!.GetValue<string>(), Does.EndWith("/hook/task.context"));
            Assert.That(preview["PermissionRequest"]![0]!["hooks"]![0]!["command"]!.GetValue<string>(), Does.EndWith("/hook/approval.requested"));
            Assert.That(preview["SubagentStop"]![0]!["hooks"]![0]!["command"]!.GetValue<string>(), Does.EndWith("/hook/subtask.idle"));
            Assert.That(preview["Stop"]![0]!["hooks"]![0]!["command"]!.GetValue<string>(), Does.EndWith("/hook/task.idle"));
            Assert.That(HookEntries.Preview(47821, Token), Does.Contain("\\\"Authorization: Bearer"), "Readable quotes, not \\u0022.");
        });
    }

    [Test]
    public void An_installation_keeps_everything_the_user_wrote()
    {
        var change = HookEntries.Install(Own, 47821, Token);

        var root = JsonNode.Parse(change.Text!)!.AsObject();
        var hooks = root["hooks"]!.AsObject();
        Assert.Multiple(() =>
        {
            Assert.That(change.Changed, Is.EqualTo(5));
            Assert.That(root["model"]!.GetValue<string>(), Is.EqualTo("opus"));
            Assert.That(root["permissions"]!["allow"]![0]!.GetValue<string>(), Is.EqualTo("Bash(git status)"));
            Assert.That(hooks["Stop"]!.AsArray(), Has.Count.EqualTo(2), "The user's Stop hook, then the one of Jarvis.");
            Assert.That(hooks["Stop"]![0]!["hooks"]![0]!["command"]!.GetValue<string>(), Does.StartWith("echo \"fini\""));
            Assert.That(hooks["PreToolUse"]![0]!["matcher"]!.GetValue<string>(), Is.EqualTo("Bash"));
            Assert.That(hooks["SessionStart"]!.AsArray(), Has.Count.EqualTo(1));
            Assert.That(HookEntries.StateOf(change.Text, 47821, Token), Is.EqualTo(HookInstallState.Installed));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  \n")]
    [TestCase("{}")]
    public void An_installation_in_an_empty_or_missing_file_writes_the_hooks_alone(string? text)
    {
        var change = HookEntries.Install(text, 47821, Token);

        Assert.Multiple(() =>
        {
            Assert.That(HookEntries.StateOf(text, 47821, Token), Is.EqualTo(HookInstallState.None));
            Assert.That(JsonNode.Parse(change.Text!)!.ToJsonString(), Is.EqualTo(JsonNode.Parse(HookEntries.Preview(47821, Token))!.ToJsonString()));
        });
    }

    [Test]
    public void Installing_twice_does_not_double_the_hooks()
    {
        var once = HookEntries.Install(Own, 47821, Token).Text;

        var twice = HookEntries.Install(once, 47821, Token).Text;

        Assert.That(twice, Is.EqualTo(once));
    }

    [Test]
    public void Another_port_or_token_makes_the_hooks_outdated_and_a_new_installation_replaces_them()
    {
        var old = HookEntries.Install(Own, 47821, Token).Text;

        var replaced = HookEntries.Install(old, 50000, "NEW").Text!;

        Assert.Multiple(() =>
        {
            Assert.That(HookEntries.StateOf(old, 50000, Token), Is.EqualTo(HookInstallState.Outdated));
            Assert.That(HookEntries.StateOf(old, 47821, "NEW"), Is.EqualTo(HookInstallState.Outdated));
            Assert.That(replaced, Does.Not.Contain(":47821/"));
            Assert.That(replaced, Does.Not.Contain(Token));
            Assert.That(HookEntries.StateOf(replaced, 50000, "NEW"), Is.EqualTo(HookInstallState.Installed));
            Assert.That(JsonNode.Parse(replaced)!["hooks"]!["Stop"]!.AsArray(), Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void A_removal_takes_away_the_lines_of_Jarvis_only_whatever_their_port()
    {
        var installed = HookEntries.Install(Own, 50000, Token).Text;

        var removed = HookEntries.Remove(installed);

        Assert.Multiple(() =>
        {
            Assert.That(removed.Changed, Is.EqualTo(5));
            Assert.That(JsonNode.Parse(removed.Text!)!.ToJsonString(), Is.EqualTo(JsonNode.Parse(Own)!.ToJsonString()), "Back to what the user wrote.");
            Assert.That(HookEntries.StateOf(removed.Text, 47821, Token), Is.EqualTo(HookInstallState.None));
        });
    }

    [Test]
    public void A_removal_from_a_file_that_held_nothing_else_leaves_an_empty_object()
    {
        var installed = HookEntries.Install(null, 47821, Token).Text;

        var removed = HookEntries.Remove(installed);

        Assert.That(JsonNode.Parse(removed.Text!)!.ToJsonString(), Is.EqualTo("{}"));
    }

    [Test]
    public void A_removal_without_hooks_of_Jarvis_changes_nothing()
    {
        var removed = HookEntries.Remove(Own);

        Assert.Multiple(() =>
        {
            Assert.That(removed.Changed, Is.Zero);
            Assert.That(JsonNode.Parse(removed.Text!)!.ToJsonString(), Is.EqualTo(JsonNode.Parse(Own)!.ToJsonString()));
        });
    }

    [Test]
    public void A_hook_of_Jarvis_inside_a_group_of_the_user_leaves_the_group()
    {
        var mixed = $$"""
            { "hooks": { "Stop": [ { "hooks": [
              { "type": "command", "command": "echo mine" },
              { "type": "command", "command": {{System.Text.Json.JsonSerializer.Serialize(HookEntries.Command(47821, Token, "/hook/task.idle"))}} }
            ] } ] } }
            """;

        var removed = HookEntries.Remove(mixed);

        var group = JsonNode.Parse(removed.Text!)!["hooks"]!["Stop"]![0]!["hooks"]!.AsArray();
        Assert.Multiple(() =>
        {
            Assert.That(HookEntries.StateOf(mixed, 47821, Token), Is.EqualTo(HookInstallState.Outdated), "One of five.");
            Assert.That(removed.Changed, Is.EqualTo(1));
            Assert.That(group, Has.Count.EqualTo(1));
            Assert.That(group[0]!["command"]!.GetValue<string>(), Is.EqualTo("echo mine"));
        });
    }

    [TestCase("{ \"model\": \"opus\", // comment\n }")]
    [TestCase("not json")]
    [TestCase("[]")]
    [TestCase("{ \"hooks\": [] }")]
    [TestCase("{ \"hooks\": \"none\" }")]
    [TestCase("{ \"hooks\": { \"Stop\": { \"hooks\": [] } } }")]
    public void A_file_Jarvis_does_not_know_how_to_read_is_never_rewritten(string text)
    {
        Assert.Multiple(() =>
        {
            Assert.That(HookEntries.Install(text, 47821, Token).Text, Is.Null);
        });
    }

    [TestCase("{ \"model\": \"opus\", // comment\n }")]
    [TestCase("[]")]
    [TestCase("{ \"hooks\": [] }")]
    public void A_file_that_is_not_settings_is_unreadable_for_the_state_and_the_removal(string text)
    {
        Assert.Multiple(() =>
        {
            Assert.That(HookEntries.StateOf(text, 47821, Token), Is.EqualTo(HookInstallState.Unreadable));
            Assert.That(HookEntries.Remove(text).Text, Is.Null);
        });
    }

    [TestCase("http://127.0.0.1:47821/hook/task.idleness")]
    [TestCase("http://127.0.0.1:47821/hooks/task.idle")]
    [TestCase("http://localhost:47821/hook/task.idle")]
    [TestCase("http://192.168.1.4:47821/hook/task.idle")]
    public void An_address_that_only_looks_like_one_of_Jarvis_is_not_removed(string address)
    {
        var text = $$"""{ "hooks": { "Stop": [ { "hooks": [ { "type": "http", "url": "{{address}}" } ] } ] } }""";

        Assert.Multiple(() =>
        {
            Assert.That(HookEntries.Remove(text).Changed, Is.Zero);
            Assert.That(HookEntries.StateOf(text, 47821, Token), Is.EqualTo(HookInstallState.None));
        });
    }
}
