// Spike E12: Claude Code sessions launched by Jarvis.
// One local HTTP server on 127.0.0.1 plays Jarvis: an MCP server (/mcp, official C# SDK, streamable HTTP) with the
// permission-prompt tool "approve", and the hook routes (/hook/<name>). The program then drives `claude -p` in
// stream-json (as in E8) in a throwaway folder and checks:
//  (a) permission requests reach the MCP tool and its answer is obeyed (deny => no file, allow => file);
//  (b) hooks SessionStart, UserPromptSubmit, PermissionRequest, Stop, SubagentStop declared in a --settings file
//      call the local routes; what each one receives; whether RELAIS_TASK_ID from the process environment is seen;
//  (c) what each --setting-sources value loads (settings, hooks, CLAUDE.md of the folder).
// Usage: dotnet run -c Release -- [scenario|all] [model]
//   scenarios: perm, src-none, src-user, src-project, src-local, src-project-local
// Nothing is written to the user's settings: Jarvis' settings are a temporary file passed with --settings.
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

const int Port = 47812;
var which = args.Length > 0 ? args[0] : "all";
var model = args.Length > 1 ? args[1] : "haiku";
var sandbox = @"C:\jarvis\temp\e12-sandbox";
var jarvisDir = @"C:\jarvis\temp\e12-jarvis";
Directory.CreateDirectory(jarvisDir);
var logFile = Path.Combine(jarvisDir, "e12-log.jsonl");
var settingsFile = Path.Combine(jarvisDir, "jarvis-claude-settings.json");
var mcpFile = Path.Combine(jarvisDir, "jarvis-mcp.json");
const string TaskId = "task-e12-0001";

PrepareSandbox();
File.WriteAllText(settingsFile, JarvisSettings().ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(mcpFile, new JsonObject
{
    ["mcpServers"] = new JsonObject { ["jarvis"] = new JsonObject { ["type"] = "http", ["url"] = $"http://127.0.0.1:{Port}/mcp" } },
}.ToJsonString());

// ---------- Jarvis' local server ----------
var builder = WebApplication.CreateBuilder();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls($"http://127.0.0.1:{Port}");
builder.Services.AddMcpServer().WithHttpTransport(o => o.Stateless = true).WithTools<ApprovalTool>();
var app = builder.Build();
app.Use(async (ctx, next) =>
{
    // Keep the raw MCP traffic: it shows the exact permission request sent by Claude Code.
    if (ctx.Request.Path.StartsWithSegments("/mcp") && ctx.Request.Method == "POST")
    {
        ctx.Request.EnableBuffering();
        var body = await new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        ctx.Request.Body.Position = 0;
        Recorder.Write(logFile, "mcp", new JsonObject(), body);
    }
    await next();
});
app.MapMcp("/mcp");
app.MapPost("/hook/{name}", async (string name, HttpRequest req) =>
{
    var body = await new StreamReader(req.Body).ReadToEndAsync();
    var headers = new JsonObject();
    foreach (var h in req.Headers.Where(h => h.Key.StartsWith("X-", StringComparison.OrdinalIgnoreCase) || h.Key is "User-Agent" or "Content-Type"))
        headers[h.Key] = h.Value.ToString();
    Recorder.Write(logFile, "hook/" + name, headers, body);
    Recorder.Hooks.Add(name);
    return Results.Ok(); // empty 2xx: no decision, the hook only observes
});
await app.StartAsync();
ApprovalTool.LogFile = logFile;

// ---------- Scenarios ----------
var scenarios = new (string Name, string? Sources, string Prompt, string[] Extra)[]
{
    ("perm", "", """
        Do these steps in order, one tool call per step, and never retry a refused step:
        1) With the Write tool, create allow-write.txt containing: ok
        2) With the Write tool, create deny-write.txt containing: no
        3) With the Bash tool, run: echo ok > allow-bash.txt
        4) With the Bash tool, run: echo no > deny-bash.txt
        5) With the Agent tool (subagent_type general-purpose), ask a subagent to reply with the single word pong.
        Then list which steps were refused, in one short sentence.
        """, ["--allowedTools", "Read,Agent"]),
    ("src-none", "", SecretPrompt(), ["--tools", ""]),
    ("src-user", "user", SecretPrompt(), ["--tools", ""]),
    ("src-project", "project", SecretPrompt(), ["--tools", ""]),
    ("src-local", "local", SecretPrompt(), ["--tools", ""]),
    ("src-project-local", "project,local", SecretPrompt(), ["--tools", ""]),
    ("src-default", null, SecretPrompt(), ["--tools", ""]),
    // CLAUDE.md without the project's hooks: no project source, the file given explicitly.
    ("src-none-append", "", SecretPrompt(), ["--tools", "", "--append-system-prompt-file", Path.Combine(sandbox, "CLAUDE.md")]),
    // Project source with disableAllHooks in Jarvis' --settings: does it also silence Jarvis' own hooks?
    ("src-project-disable", "project", SecretPrompt(), ["--tools", "", "--settings", """{"disableAllHooks": true}"""]),
};
var summary = new List<string>();
foreach (var s in scenarios.Where(s => which == "all" || s.Name == which))
{
    Recorder.Hooks.Clear(); ApprovalTool.Calls.Clear();
    foreach (var f in Directory.GetFiles(sandbox, "*-*.txt")) File.Delete(f);
    var result = await RunClaude(s.Name, s.Sources, s.Prompt, s.Extra);
    var files = Directory.GetFiles(sandbox, "*-*.txt").Select(Path.GetFileName).Order().ToList();
    var line = $"[{s.Name}] sources={(s.Sources is null ? "(flag absent)" : $"\"{s.Sources}\"")} exit={result.Exit} answer={result.Answer} cost=${result.Cost} " +
               $"hooks=[{string.Join(",", Recorder.Hooks)}] approve_calls=[{string.Join(" ; ", ApprovalTool.Calls)}] files=[{string.Join(",", files)}] " +
               $"init={result.Init} denials={result.Denials} stderr={result.Stderr}";
    summary.Add(line);
    Console.WriteLine(line + "\n");
}
File.WriteAllLines(Path.Combine(jarvisDir, "e12-summary.txt"), summary);
await app.StopAsync();

// ---------- Helpers ----------
string SecretPrompt() => "What is the project's secret word given in your instructions (CLAUDE.md)? Reply with only the word, or NONE if you were given none.";

async Task<(int Exit, string Answer, string Cost, string Init, string Denials, string Stderr)> RunClaude(string name, string? sources, string prompt, string[] extra)
{
    var psi = new ProcessStartInfo("claude")
    {
        WorkingDirectory = sandbox, UseShellExecute = false,
        RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
    };
    psi.Environment["RELAIS_TASK_ID"] = TaskId; // ADR 0013, section 7: the task is created before the launch
    var argv = new List<string>
    {
        "-p", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose", "--include-hook-events",
        "--model", model, "--max-budget-usd", "0.50", "--max-turns", "12", "--no-session-persistence",
        "--settings", settingsFile,
        "--mcp-config", mcpFile, "--strict-mcp-config",
        "--permission-prompt-tool", "mcp__jarvis__approve",
    };
    if (sources is not null) { argv.Add("--setting-sources"); argv.Add(sources); }
    argv.AddRange(extra);
    argv.ForEach(psi.ArgumentList.Add);
    Recorder.Write(logFile, "launch/" + name, new JsonObject(), string.Join(" ", argv.Select(a => a.Contains(' ') || a.Length == 0 ? $"\"{a}\"" : a)));

    using var claude = Process.Start(psi)!;
    var stderrTask = claude.StandardError.ReadToEndAsync();
    await claude.StandardInput.WriteLineAsync(new JsonObject
    {
        ["type"] = "user", ["message"] = new JsonObject { ["role"] = "user", ["content"] = prompt },
    }.ToJsonString());
    claude.StandardInput.Close();
    string answer = "", cost = "", init = "", denials = "";
    while (await claude.StandardOutput.ReadLineAsync() is { } line)
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        JsonNode? e; try { e = JsonNode.Parse(line); } catch (JsonException) { continue; }
        var type = e?["type"]?.GetValue<string>() + (e?["subtype"] is JsonValue st ? "/" + st : "");
        Recorder.Write(logFile, "event/" + name, new JsonObject(), line);
        if (type == "system/init")
            init = $"{{model:{e!["model"]}, permissionMode:{e["permissionMode"]}, tools:{e["tools"]?.AsArray().Count}, mcp:{e["mcp_servers"]?.ToJsonString()}, plugins:{e["plugins"]?.AsArray().Count}, slash:{e["slash_commands"]?.AsArray().Count}, agents:{e["agents"]?.AsArray().Count}, skills:{e["skills"]?.AsArray().Count}}}";
        if (type?.StartsWith("result") == true)
        {
            answer = e!["result"]?.ToString().Replace("\n", " ") ?? "";
            cost = e["total_cost_usd"]?.ToString() ?? "";
            denials = e["permission_denials"]?.AsArray().Count.ToString() ?? "0";
        }
    }
    await claude.WaitForExitAsync();
    var stderr = (await stderrTask).Trim().Replace("\n", " ");
    return (claude.ExitCode, answer, cost, init, denials, stderr.Length > 300 ? stderr[..300] : stderr);
}

void PrepareSandbox()
{
    Directory.CreateDirectory(Path.Combine(sandbox, ".claude"));
    File.WriteAllText(Path.Combine(sandbox, "CLAUDE.md"), "# E12 sandbox\n\nThe project's secret word is CAPYBARA-7.\n");
    // Project and local settings each declare hooks to their own routes, to see which ones run.
    // (An http hook on SessionStart never fires: SessionStart only runs command hooks, so a command hook is used there.)
    JsonObject HookSettings(string who) => new()
    {
        ["hooks"] = new JsonObject
        {
            ["SessionStart"] = new JsonArray(new JsonObject
            {
                ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = $"curl -s -X POST --data-binary @- http://127.0.0.1:{Port}/hook/{who}.session-start" }),
            }),
            ["UserPromptSubmit"] = new JsonArray(new JsonObject
            {
                ["hooks"] = new JsonArray(new JsonObject { ["type"] = "http", ["url"] = $"http://127.0.0.1:{Port}/hook/{who}.prompt" }),
            }),
        },
    };
    File.WriteAllText(Path.Combine(sandbox, ".claude", "settings.json"), HookSettings("project").ToJsonString());
    File.WriteAllText(Path.Combine(sandbox, ".claude", "settings.local.json"), HookSettings("local").ToJsonString());
    if (!Directory.Exists(Path.Combine(sandbox, ".git")))
        Process.Start(new ProcessStartInfo("git", "init -q") { WorkingDirectory = sandbox, UseShellExecute = false })!.WaitForExit();
}

JsonObject JarvisSettings()
{
    // Jarvis' own Claude Code settings (ADR 0009, section 7): only hooks to the local routes of ADR 0013, section 7.
    JsonObject Http(string route) => new()
    {
        ["type"] = "http", ["url"] = $"http://127.0.0.1:{Port}/hook/{route}", ["timeout"] = 5,
        ["headers"] = new JsonObject { ["X-Relais-Task"] = "$RELAIS_TASK_ID" },
        ["allowedEnvVars"] = new JsonArray("RELAIS_TASK_ID"),
    };
    JsonArray Event(params JsonObject[] hooks) => new(new JsonObject { ["hooks"] = new JsonArray(hooks) });
    return new JsonObject
    {
        ["hooks"] = new JsonObject
        {
            ["SessionStart"] = Event(Http("task.context"), new JsonObject
            {
                // Same event as a command hook, to see whether the process environment reaches it.
                ["type"] = "command",
                ["command"] = $"curl -s -X POST -H \"Content-Type: application/json\" -H \"X-Relais-Task: $RELAIS_TASK_ID\" --data-binary @- http://127.0.0.1:{Port}/hook/command.session-start",
            }),
            ["UserPromptSubmit"] = Event(Http("task.start")),
            ["PermissionRequest"] = Event(Http("approval.requested")),
            ["Stop"] = Event(Http("task.idle")),
            ["SubagentStop"] = Event(Http("subtask.idle")),
        },
    };
}

/// <summary>Jarvis' permission prompt tool. Policy of the spike: refuse anything whose input mentions "deny".</summary>
[McpServerToolType]
public sealed class ApprovalTool
{
    public static string LogFile = "";
    public static readonly List<string> Calls = [];

    [McpServerTool(Name = "approve"), Description("Asks Jarvis (the user) whether Claude Code may use a tool.")]
    public static string Approve(
        [Description("Name of the tool Claude Code wants to use")] string tool_name,
        [Description("Input of that tool")] JsonElement input,
        [Description("Id of the tool use")] string? tool_use_id = null)
    {
        var raw = input.GetRawText();
        var deny = raw.Contains("deny", StringComparison.OrdinalIgnoreCase);
        var answer = deny
            ? new JsonObject { ["behavior"] = "deny", ["message"] = "Refusé par Jarvis (essai E12)." }
            : new JsonObject { ["behavior"] = "allow", ["updatedInput"] = JsonNode.Parse(raw) };
        lock (Calls) Calls.Add($"{tool_name}:{(deny ? "deny" : "allow")}");
        Recorder.Write(LogFile, "approve", new JsonObject { ["tool_use_id"] = tool_use_id }, answer.ToJsonString());
        return answer.ToJsonString();
    }
}

static class Recorder
{
    public static readonly List<string> Hooks = [];
    static readonly object Gate = new();
    public static void Write(string file, string route, JsonObject headers, string body)
    {
        var line = new JsonObject { ["at"] = DateTime.Now.ToString("HH:mm:ss.fff"), ["route"] = route, ["headers"] = headers, ["body"] = body };
        lock (Gate) File.AppendAllText(file, line.ToJsonString() + Environment.NewLine);
    }
}
