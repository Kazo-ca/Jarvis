using System.Text.Json;
using Kazo.Jarvis.Adapters.ClaudeCode;
using Kazo.Jarvis.Kernel.ClaudeCode;

namespace Kazo.Jarvis.Tests.Unit.Adapters.ClaudeCode;

/// <summary>
/// The command line of ADR 0021, section 1, and the two files of Jarvis: read only by default, nothing of the
/// user nor of the repository unless the folder is trusted, and no secret in a file.
/// </summary>
[TestFixture]
public sealed class ClaudeCommandLineTests
{
    private static readonly ClaudeLaunch Launch = new("task-1", "/src/bar", "Corrige le menu");

    [Test]
    public void By_default_nothing_of_the_user_nor_of_the_repository_is_loaded_and_the_session_is_read_only()
    {
        var arguments = ClaudeCommandLine.Arguments(Launch, "/j/settings.json", "/j/mcp.json", "/src/bar/CLAUDE.md");

        Assert.That(arguments, Is.EqualTo(new[]
        {
            "-p", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose",
            "--setting-sources", "",
            "--append-system-prompt-file", "/src/bar/CLAUDE.md",
            "--settings", "/j/settings.json",
            "--mcp-config", "/j/mcp.json", "--strict-mcp-config",
            "--permission-prompt-tool", "mcp__jarvis__approve",
            "--allowedTools", "Read,Glob,Grep",
        }));
    }

    [Test]
    public void Without_a_CLAUDE_md_no_instructions_file_is_given()
    {
        var arguments = ClaudeCommandLine.Arguments(Launch, "s", "m", null);

        Assert.That(arguments, Has.None.EqualTo("--append-system-prompt-file"));
    }

    [Test]
    public void A_trusted_folder_loads_the_project_and_its_CLAUDE_md_is_not_given_twice()
    {
        var arguments = ClaudeCommandLine.Arguments(Launch with { Trusted = true }, "s", "m", "/src/bar/CLAUDE.md");

        Assert.Multiple(() =>
        {
            Assert.That(arguments[arguments.ToList().IndexOf("--setting-sources") + 1], Is.EqualTo("project"));
            Assert.That(arguments, Has.None.EqualTo("--append-system-prompt-file"));
            Assert.That(arguments, Has.None.EqualTo("user"), "The settings and the hooks of the user are never loaded.");
        });
    }

    [Test]
    public void The_budget_the_model_the_other_folders_and_the_session_to_resume_are_passed()
    {
        var launch = Launch with { BudgetUsd = 2m, Model = "sonnet", MoreFolders = ["/src/lib", "/docs"], ResumeSession = "s-9" };

        var arguments = string.Join(' ', ClaudeCommandLine.Arguments(launch, "s", "m", null));

        Assert.Multiple(() =>
        {
            Assert.That(arguments, Does.Contain("--add-dir /src/lib --add-dir /docs"));
            Assert.That(arguments, Does.Contain("--max-budget-usd 2.00"));
            Assert.That(arguments, Does.Contain("--model sonnet"));
            Assert.That(arguments, Does.EndWith("--resume s-9"));
        });
    }

    [Test]
    public void Without_a_budget_no_budget_option_is_passed()
    {
        Assert.That(ClaudeCommandLine.Arguments(Launch, "s", "m", null), Has.None.EqualTo("--max-budget-usd"));
    }

    [Test]
    public void The_settings_file_declares_http_hooks_that_name_the_token_and_the_task_by_variables()
    {
        using var settings = JsonDocument.Parse(ClaudeCommandLine.Settings(47821));

        var hooks = settings.RootElement.GetProperty("hooks");
        var stop = hooks.GetProperty("Stop")[0].GetProperty("hooks")[0];
        Assert.Multiple(() =>
        {
            Assert.That(hooks.EnumerateObject().Select(pair => pair.Name), Is.EqualTo(new[] { "UserPromptSubmit", "PermissionRequest", "SubagentStop", "Stop" }),
                "SessionStart takes no http hook (spike E12): the session is known from the stream.");
            Assert.That(stop.GetProperty("type").GetString(), Is.EqualTo("http"));
            Assert.That(stop.GetProperty("url").GetString(), Is.EqualTo("http://127.0.0.1:47821/hook/task.idle"));
            Assert.That(stop.GetProperty("headers").GetProperty("Authorization").GetString(), Is.EqualTo("Bearer $JARVIS_LOCAL_TOKEN"));
            Assert.That(stop.GetProperty("headers").GetProperty("X-Relais-Task").GetString(), Is.EqualTo("$RELAIS_TASK_ID"));
            Assert.That(stop.GetProperty("allowedEnvVars").EnumerateArray().Select(name => name.GetString()), Is.EquivalentTo(new[] { "JARVIS_LOCAL_TOKEN", "RELAIS_TASK_ID" }));
            Assert.That(hooks.GetProperty("PermissionRequest")[0].GetProperty("hooks")[0].GetProperty("url").GetString(), Does.EndWith("/hook/approval.requested"));
        });
    }

    [Test]
    public void The_MCP_file_declares_the_server_of_Jarvis_alone()
    {
        using var mcp = JsonDocument.Parse(ClaudeCommandLine.Mcp(47821, "/mcp"));

        var servers = mcp.RootElement.GetProperty("mcpServers");
        var jarvis = servers.GetProperty("jarvis");
        Assert.Multiple(() =>
        {
            Assert.That(servers.EnumerateObject().Count(), Is.EqualTo(1));
            Assert.That(jarvis.GetProperty("type").GetString(), Is.EqualTo("http"));
            Assert.That(jarvis.GetProperty("url").GetString(), Is.EqualTo("http://127.0.0.1:47821/mcp"));
            Assert.That(jarvis.GetProperty("headers").GetProperty("Authorization").GetString(), Is.EqualTo("Bearer ${JARVIS_LOCAL_TOKEN}"));
            Assert.That(jarvis.GetProperty("headers").GetProperty("X-Relais-Task").GetString(), Is.EqualTo("${RELAIS_TASK_ID}"));
        });
    }
}
