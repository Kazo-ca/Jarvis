using Kazo.Jarvis.Adapters.ClaudeCode;
using Kazo.Jarvis.Kernel.ClaudeCode;

namespace Kazo.Jarvis.Tests.Unit.Adapters.ClaudeCode;

/// <summary>
/// The lines of <c>--output-format stream-json</c> (spikes E8 and E12): the start, the tools used and run, the
/// end of a turn with its cost; a line that is not understood gives nothing.
/// </summary>
[TestFixture]
public sealed class ClaudeStreamTests
{
    [Test]
    public void The_start_gives_the_session_and_the_model()
    {
        var events = ClaudeStream.Read("""{"type":"system","subtype":"init","session_id":"s-1","model":"claude-haiku-5-5","tools":["Read"]}""");

        Assert.That(events, Is.EqualTo(new ClaudeRunEvent[] { new ClaudeStarted("s-1", "claude-haiku-5-5") }));
    }

    [Test]
    public void A_message_of_the_assistant_gives_each_tool_it_wants_to_use()
    {
        var events = ClaudeStream.Read("""
            {"type":"assistant","message":{"content":[{"type":"text","text":"I write."},{"type":"tool_use","id":"toolu_1","name":"Write","input":{"file_path":"a.txt","content":"x"}},{"type":"tool_use","id":"toolu_2","name":"Bash","input":{"command":"ls"}}]}}
            """.Trim());

        Assert.That(events, Has.Count.EqualTo(2));
        var first = (ClaudeToolUse)events[0];
        Assert.Multiple(() =>
        {
            Assert.That(first.ToolUseId, Is.EqualTo("toolu_1"));
            Assert.That(first.Name, Is.EqualTo("Write"));
            Assert.That(first.Input.GetProperty("file_path").GetString(), Is.EqualTo("a.txt"));
            Assert.That(((ClaudeToolUse)events[1]).Name, Is.EqualTo("Bash"));
        });
    }

    [Test]
    public void A_tool_result_tells_whether_the_tool_ran()
    {
        var events = ClaudeStream.Read("""
            {"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_1","content":"ok"},{"type":"tool_result","tool_use_id":"toolu_2","is_error":true,"content":"denied"}]}}
            """.Trim());

        Assert.That(events, Is.EqualTo(new ClaudeRunEvent[] { new ClaudeToolResult("toolu_1", false), new ClaudeToolResult("toolu_2", true) }));
    }

    [Test]
    public void The_end_of_a_turn_gives_the_text_the_cost_and_the_denials()
    {
        var events = ClaudeStream.Read("""{"type":"result","subtype":"success","is_error":false,"result":"C'est fait.","total_cost_usd":0.009123,"permission_denials":[{"tool_name":"Write"},{"tool_name":"Bash"}]}""");

        Assert.That(events, Is.EqualTo(new ClaudeRunEvent[] { new ClaudeTurnEnded("C'est fait.", 0.009123m, false, 2) }));
    }

    [TestCase("""{"type":"result","subtype":"error_max_budget_usd","is_error":false}""")]
    [TestCase("""{"type":"result","subtype":"success","is_error":true,"result":"x"}""")]
    public void A_turn_that_did_not_succeed_is_an_error(string line)
    {
        var ended = (ClaudeTurnEnded)ClaudeStream.Read(line).Single();

        Assert.Multiple(() =>
        {
            Assert.That(ended.IsError, Is.True);
            Assert.That(ended.CostUsd, Is.Null);
            Assert.That(ended.Denials, Is.Zero);
        });
    }

    [TestCase("")]
    [TestCase("not json")]
    [TestCase("[1]")]
    [TestCase("""{"type":"system","subtype":"hook_started"}""")]
    [TestCase("""{"type":"system","subtype":"init"}""")]
    [TestCase("""{"type":"assistant","message":"text"}""")]
    [TestCase("""{"type":"assistant","message":{"content":[{"type":"tool_use","name":"NoId"}]}}""")]
    [TestCase("""{"type":"stream_event"}""")]
    public void A_line_that_is_not_understood_gives_nothing(string line)
    {
        Assert.That(ClaudeStream.Read(line), Is.Empty);
    }

    [Test]
    public void A_negative_cost_is_not_a_cost()
    {
        var ended = (ClaudeTurnEnded)ClaudeStream.Read("""{"type":"result","subtype":"success","total_cost_usd":-1}""").Single();

        Assert.That(ended.CostUsd, Is.Null);
    }

    [Test]
    public void The_message_of_the_user_is_one_line_of_JSON()
    {
        var line = ClaudeStream.UserMessage("Corrige « le menu »\net dis \"ok\".");

        using var document = System.Text.Json.JsonDocument.Parse(line);
        Assert.Multiple(() =>
        {
            Assert.That(line, Does.Not.Contain("\n"));
            Assert.That(document.RootElement.GetProperty("type").GetString(), Is.EqualTo("user"));
            Assert.That(document.RootElement.GetProperty("message").GetProperty("role").GetString(), Is.EqualTo("user"));
            Assert.That(document.RootElement.GetProperty("message").GetProperty("content").GetString(), Is.EqualTo("Corrige « le menu »\net dis \"ok\"."));
        });
    }
}
