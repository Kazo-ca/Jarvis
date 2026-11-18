// Spike E8: drive Claude Code from C# through its headless CLI (no Node/Python bridge).
// Starts `claude -p` with stream-json in and out, sends one request, prints every event type it receives,
// and checks that writes are refused when only read tools are allowed (permission model of US2.10).
// Usage: dotnet run -c Release -- [working folder]
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

var folder = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "jarvis-e8");
Directory.CreateDirectory(folder);
File.WriteAllText(Path.Combine(folder, "note.txt"), "Le code du portail est 4711.");

var psi = new ProcessStartInfo("claude")
{
    WorkingDirectory = folder,
    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
    UseShellExecute = false,
};
foreach (var a in new[] { "-p", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose",
                          "--allowedTools", "Read", "--permission-mode", "default", "--max-turns", "6" })
    psi.ArgumentList.Add(a);

var sw = Stopwatch.StartNew();
using var claude = Process.Start(psi)!;
var request = "Lis note.txt et donne-moi le code en une phrase. Ensuite, essaie d'écrire « ok » dans done.txt.";
var message = new JsonObject
{
    ["type"] = "user",
    ["message"] = new JsonObject { ["role"] = "user", ["content"] = request },
};
await claude.StandardInput.WriteLineAsync(message.ToJsonString());
claude.StandardInput.Close(); // one request; a long-lived session would keep stdin open and send more turns

string? sessionId = null; var types = new List<string>();
while (await claude.StandardOutput.ReadLineAsync() is { } line)
{
    if (string.IsNullOrWhiteSpace(line)) continue;
    var e = JsonNode.Parse(line)!;
    var type = e["type"]?.GetValue<string>() + (e["subtype"] is { } st ? "/" + st.GetValue<string>() : "");
    types.Add(type);
    sessionId ??= e["session_id"]?.GetValue<string>();
    switch (type)
    {
        case "assistant":
            foreach (var block in e["message"]!["content"]!.AsArray())
                Console.WriteLine(block!["type"]!.GetValue<string>() == "tool_use"
                    ? $"{sw.ElapsedMilliseconds,6} ms  tool_use {block["name"]} {block["input"]?.ToJsonString()}"
                    : $"{sw.ElapsedMilliseconds,6} ms  text: {block["text"]}");
            break;
        case var t when t.StartsWith("result"):
            Console.WriteLine($"{sw.ElapsedMilliseconds,6} ms  result: {e["result"]} | cost ${e["total_cost_usd"]} | denials {e["permission_denials"]?.ToJsonString()}");
            break;
    }
}
await claude.WaitForExitAsync();
Console.WriteLine($"exit {claude.ExitCode}, session {sessionId}, events: {string.Join(", ", types.Distinct())}");
var err = await claude.StandardError.ReadToEndAsync();
if (err.Length > 0) Console.WriteLine("stderr: " + err.Trim());
