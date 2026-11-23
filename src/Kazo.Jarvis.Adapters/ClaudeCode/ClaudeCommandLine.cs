using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kazo.Jarvis.Kernel.ClaudeCode;
using Kazo.Jarvis.Kernel.LocalServer;

namespace Kazo.Jarvis.Adapters.ClaudeCode;

/// <summary>
/// The command line of a session started by Jarvis and the two files it points to (ADR 0021, sections 1, 2, 3
/// and 5). Pure text: nothing here starts a process or writes a file. Neither file holds a secret: the token of
/// the run and the task are named by environment variables, which only the process Jarvis starts receives.
/// </summary>
internal static class ClaudeCommandLine
{
    /// <summary>The variable that gives a session its task (ADR 0013, section 7).</summary>
    public const string TaskVariable = "RELAIS_TASK_ID";

    /// <summary>The variable that gives a session the token of the run.</summary>
    public const string TokenVariable = "JARVIS_LOCAL_TOKEN";

    /// <summary>The permission tool, as Claude Code names a tool of the MCP server « jarvis ».</summary>
    public const string PermissionTool = "mcp__jarvis__approve";

    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    /// <summary>The arguments of <c>claude</c> for one launch.</summary>
    /// <param name="launch">What to start.</param>
    /// <param name="settingsFile">The settings file of Jarvis.</param>
    /// <param name="mcpFile">The MCP file of Jarvis.</param>
    /// <param name="instructionsFile">The <c>CLAUDE.md</c> at the root of the folder, when it exists.</param>
    /// <returns>The arguments, one per item.</returns>
    public static IReadOnlyList<string> Arguments(ClaudeLaunch launch, string settingsFile, string mcpFile, string? instructionsFile)
    {
        List<string> arguments =
        [
            "-p", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose",

            // Nothing of the user nor of the repository by default; the whole project for a trusted folder, hooks included.
            "--setting-sources", launch.Trusted ? "project" : string.Empty,
        ];
        if (!launch.Trusted && instructionsFile is not null)
        {
            arguments.AddRange(["--append-system-prompt-file", instructionsFile]);
        }

        arguments.AddRange(
        [
            "--settings", settingsFile,
            "--mcp-config", mcpFile, "--strict-mcp-config",
            "--permission-prompt-tool", PermissionTool,

            // Read only: anything else asks the permission tool.
            "--allowedTools", "Read,Glob,Grep",
        ]);
        foreach (var folder in launch.MoreFolders)
        {
            arguments.AddRange(["--add-dir", folder]);
        }

        if (launch.BudgetUsd > 0)
        {
            arguments.AddRange(["--max-budget-usd", launch.BudgetUsd.ToString("0.00", CultureInfo.InvariantCulture)]);
        }

        if (!string.IsNullOrWhiteSpace(launch.Model))
        {
            arguments.AddRange(["--model", launch.Model]);
        }

        if (!string.IsNullOrWhiteSpace(launch.ResumeSession))
        {
            arguments.AddRange(["--resume", launch.ResumeSession]);
        }

        return arguments;
    }

    /// <summary>
    /// The settings file of Jarvis: the hooks of ADR 0021, section 5, as <c>http</c> hooks. <c>SessionStart</c> is
    /// left out: it only takes commands, whose variable syntax depends on the shell, and Jarvis already learns the
    /// session from the start of the stream.
    /// </summary>
    /// <param name="port">The port of the local server.</param>
    /// <returns>The JSON text.</returns>
    public static string Settings(int port)
    {
        JsonArray Hook(string path) => new(new JsonObject
        {
            ["hooks"] = new JsonArray(new JsonObject
            {
                ["type"] = "http",
                ["url"] = $"http://127.0.0.1:{port}{path}",
                ["timeout"] = 5,
                ["headers"] = new JsonObject
                {
                    ["Authorization"] = $"Bearer ${TokenVariable}",
                    [LocalRequest.TaskHeader] = $"${TaskVariable}",
                },
                ["allowedEnvVars"] = new JsonArray(TokenVariable, TaskVariable),
            }),
        });

        return new JsonObject
        {
            ["hooks"] = new JsonObject
            {
                ["UserPromptSubmit"] = Hook(HookPaths.Start),
                ["PermissionRequest"] = Hook(HookPaths.Approval),
                ["SubagentStop"] = Hook(HookPaths.SubtaskIdle),
                ["Stop"] = Hook(HookPaths.Idle),
            },
        }.ToJsonString(Layout);
    }

    /// <summary>The MCP file of Jarvis: its own server and no other.</summary>
    /// <param name="port">The port of the local server.</param>
    /// <param name="path">The path of the MCP server.</param>
    /// <returns>The JSON text.</returns>
    public static string Mcp(int port, string path) => new JsonObject
    {
        ["mcpServers"] = new JsonObject
        {
            ["jarvis"] = new JsonObject
            {
                ["type"] = "http",
                ["url"] = $"http://127.0.0.1:{port}{path}",
                ["headers"] = new JsonObject
                {
                    ["Authorization"] = $"Bearer ${{{TokenVariable}}}",
                    [LocalRequest.TaskHeader] = $"${{{TaskVariable}}}",
                },
            },
        },
    }.ToJsonString(Layout);
}
