// Spike E2/E3: list the Claude Code sessions of this PC (terminal and Claude Desktop)
// and follow their state without touching them. Read-only: never writes into ~/.claude.
using System.Diagnostics;
using System.Text.Json;

var claudeHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
var registry = Path.Combine(claudeHome, "sessions");
var projects = Path.Combine(claudeHome, "projects");
var watchSeconds = args.Length > 1 && args[0] == "--watch" ? int.Parse(args[1]) : 0;

var sw = Stopwatch.StartNew();
var sessions = SessionReader.ReadAll(registry, projects);
Console.WriteLine($"{sessions.Count} sessions read in {sw.ElapsedMilliseconds} ms");
foreach (var s in sessions.OrderByDescending(s => s.StatusAt))
    Console.WriteLine(s.Describe());

if (watchSeconds == 0) return;

// Watch mode: report every state transition (busy -> idle = turn finished, -> waiting = needs the user).
var last = sessions.ToDictionary(s => s.SessionId, s => s.State);
using var watcher = new FileSystemWatcher(registry, "*.json") { EnableRaisingEvents = true };
watcher.Changed += (_, e) => Report(e.FullPath);
watcher.Created += (_, e) => Report(e.FullPath);
watcher.Deleted += (_, e) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} ended  {e.Name}");
Console.WriteLine($"Watching for {watchSeconds} s...");
await Task.Delay(TimeSpan.FromSeconds(watchSeconds));

void Report(string path)
{
    var s = SessionReader.TryRead(path, projects);
    if (s is null) return;
    lock (last)
    {
        if (last.TryGetValue(s.SessionId, out var before) && before == s.State) return;
        last[s.SessionId] = s.State;
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} {before ?? "new"} -> {s.State}  {s.Name}");
    }
}

/// <summary>One Claude Code process as seen from its registry file and transcript.</summary>
record SessionInfo(string SessionId, int Pid, bool Alive, string State, string? WaitingFor, string Entrypoint,
    string Cwd, string Name, DateTimeOffset StatusAt, string? LastAssistantText, DateTimeOffset? LastActivity)
{
    public string Describe() =>
        $"[{State,-8}] {(Alive ? "" : "(dead) ")}{Entrypoint,-15} {StatusAt.LocalDateTime:MM-dd HH:mm} {Trim(Name, 45),-45} | {Trim(LastAssistantText, 60)}"
        + (WaitingFor is null ? "" : $" (waiting for: {WaitingFor})");
    static string Trim(string? s, int n) => s is null ? "" : (s.Length <= n ? s : s[..n] + "…").ReplaceLineEndings(" ");
}

static class SessionReader
{
    public static List<SessionInfo> ReadAll(string registry, string projects) =>
        Directory.EnumerateFiles(registry, "*.json").Select(f => TryRead(f, projects)).OfType<SessionInfo>().ToList();

    /// <summary>Reads one registry file. Files are rewritten by Claude Code, so a partial read is skipped, not fatal.</summary>
    public static SessionInfo? TryRead(string file, string projects)
    {
        try
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(fs);
            var r = doc.RootElement;
            var id = r.GetProperty("sessionId").GetString()!;
            var pid = r.GetProperty("pid").GetInt32();
            var cwd = r.GetProperty("cwd").GetString()!;
            var (text, at) = LastAssistant(projects, cwd, id);
            return new SessionInfo(id, pid, IsAlive(pid),
                Str(r, "status") ?? "unknown", Str(r, "waitingFor"), Str(r, "entrypoint") ?? "?", cwd,
                Str(r, "name") ?? Path.GetFileName(cwd),
                DateTimeOffset.FromUnixTimeMilliseconds(r.TryGetProperty("statusUpdatedAt", out var t) ? t.GetInt64() : 0),
                text, at);
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException) { return null; }
    }

    static string? Str(JsonElement r, string name) =>
        r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static bool IsAlive(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch (ArgumentException) { return false; }
    }

    /// <summary>Transcript path: projects/&lt;cwd with every non-alphanumeric char replaced by '-'&gt;/&lt;sessionId&gt;.jsonl.
    /// Only the tail is read, so a long transcript costs the same as a short one.</summary>
    static (string?, DateTimeOffset?) LastAssistant(string projects, string cwd, string id)
    {
        var slug = new string(cwd.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        var path = Path.Combine(projects, slug, id + ".jsonl");
        if (!File.Exists(path)) return (null, null);
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        const int tail = 256 * 1024;
        if (fs.Length > tail) fs.Seek(-tail, SeekOrigin.End);
        var lines = new StreamReader(fs).ReadToEnd().Split('\n');
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            try
            {
                using var d = JsonDocument.Parse(lines[i]);
                var r = d.RootElement;
                if (Str(r, "type") != "assistant") continue;
                var content = r.GetProperty("message").GetProperty("content");
                foreach (var block in content.EnumerateArray())
                    if (Str(block, "type") == "text")
                        return (Str(block, "text"), DateTimeOffset.Parse(Str(r, "timestamp")!));
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { }
        }
        return (null, null);
    }
}
