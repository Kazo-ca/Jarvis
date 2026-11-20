// Spike E14: MCP client in C# (official ModelContextProtocol package) against the MCP server of Home Assistant.
// Reads host and token from a local file outside the repo (same file as E9): "Host: https://..." and "Token: ...".
// Usage: dotnet run -c Release -- <step> [args]
//   transport          connect with streamable HTTP, then with SSE, and report which works
//   tools              list the tools with their schema, and show them as Microsoft.Extensions.AI AIFunction
//   context            one read call (GetLiveContext): entities per domain, areas, security-sensitive domains
//   light "<name>"     turn one light on, check, then put it back off (only if it was off), check
//   reconnect          a call, a simulated network cut, a call during the cut, then calls after it
// Never calls scripts, locks, alarms, covers or climate.
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var step = args.Length > 0 ? args[0] : "tools";
var text = File.ReadAllText(@"C:\jarvis\temp\home-assistant.md");
var host = Regex.Match(text, @"Host:\s*(\S+)").Groups[1].Value.TrimEnd('/');
var token = Regex.Match(text, @"Token:\s*`?([^`\s]+)").Groups[1].Value;
var cut = new CuttableHandler { InnerHandler = new HttpClientHandler() };
var http = new HttpClient(cut) { Timeout = TimeSpan.FromSeconds(30) };

async Task<McpClient> Connect(HttpTransportMode mode, string path)
{
    var transport = new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(host + path),
        TransportMode = mode,
        Name = "home-assistant",
        AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + token },
    }, http);
    return await McpClient.CreateAsync(transport);
}

switch (step)
{
    case "transport":
        foreach (var (mode, path) in new[] { (HttpTransportMode.StreamableHttp, "/api/mcp"), (HttpTransportMode.Sse, "/api/mcp"), (HttpTransportMode.Sse, "/mcp_server/sse"), (HttpTransportMode.AutoDetect, "/api/mcp") })
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await using var c = await Connect(mode, path);
                var tools = await c.ListToolsAsync();
                Console.WriteLine($"{mode,-15} {path,-16} OK in {sw.ElapsedMilliseconds} ms: server {c.ServerInfo.Name} {c.ServerInfo.Version}, protocol {c.NegotiatedProtocolVersion}, session {(c.SessionId is null ? "none" : "yes")}, {tools.Count} tools");
            }
            catch (Exception ex) { Console.WriteLine($"{mode,-15} {path,-16} FAILED in {sw.ElapsedMilliseconds} ms: {ex.GetType().Name}: {First(ex.Message)}"); }
        }
        break;

    case "tools":
    {
        await using var c = await Connect(HttpTransportMode.StreamableHttp, "/api/mcp");
        var tools = await c.ListToolsAsync();
        Console.WriteLine($"server {c.ServerInfo.Name} {c.ServerInfo.Version}, capabilities: tools={c.ServerCapabilities.Tools is not null} prompts={c.ServerCapabilities.Prompts is not null} resources={c.ServerCapabilities.Resources is not null} logging={c.ServerCapabilities.Logging is not null}");
        foreach (var t in tools)
        {
            AIFunction f = t; // McpClientTool derives from AIFunction (Microsoft.Extensions.AI)
            Console.WriteLine($"\n{f.Name}: {First(f.Description)}\n  schema: {f.JsonSchema.GetRawText()}");
            if (t.ProtocolTool.Annotations is { } a) Console.WriteLine($"  annotations: readOnly={a.ReadOnlyHint} destructive={a.DestructiveHint} idempotent={a.IdempotentHint}");
        }
        IList<AITool> forChat = [.. tools];
        var options = new ChatOptions { Tools = forChat };
        Console.WriteLine($"\n{tools.Count} tools, all AIFunction: {tools.All(t => t is AIFunction)}; ChatOptions.Tools holds {options.Tools.Count}");
        var scripts = tools.Where(t => t.Name.StartsWith("script")).Count();
        Console.WriteLine($"tools that are Home Assistant scripts (one MCP tool per exposed script): {scripts}");
        break;
    }

    case "context":
    {
        await using var c = await Connect(HttpTransportMode.StreamableHttp, "/api/mcp");
        var sw = Stopwatch.StartNew();
        var r = await c.CallToolAsync("homeassistant__GetLiveContext", new Dictionary<string, object?>());
        Console.WriteLine($"GetLiveContext in {sw.ElapsedMilliseconds} ms, isError={r.IsError}, {r.Content.Count} content block(s)");
        var body = string.Join("\n", r.Content.OfType<TextContentBlock>().Select(b => b.Text));
        var live = Unwrap(body);
        Console.WriteLine("first lines:\n" + string.Join("\n", live.Split('\n').Take(14)));
        var entities = Entities(live);
        Console.WriteLine($"\n{entities.Count} exposed entities. Per domain: {string.Join(", ", entities.GroupBy(e => e.Domain).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}"))}");
        Console.WriteLine($"fields seen per entity: {string.Join(", ", Regex.Matches(live, @"^\s{2}(\w+):", RegexOptions.Multiline).Select(m => m.Groups[1].Value).Distinct())}");
        Console.WriteLine($"with an area: {entities.Count(e => e.Area is not null)}");
        string[] security = ["lock", "alarm_control_panel", "cover", "climate", "camera", "siren", "valve"];
        foreach (var d in security)
            Console.WriteLine($"security-sensitive domain {d}: {entities.Count(e => e.Domain == d)} entities{(d == "cover" ? $" ({string.Join("; ", entities.Where(e => e.Domain == d).Select(e => $"device_class={e.DeviceClass ?? "?"}"))})" : "")}");
        Console.WriteLine("\nlights (name | state | area):");
        foreach (var e in entities.Where(e => e.Domain == "light")) Console.WriteLine($"  {e.Name} | {e.State} | {e.Area}");
        break;
    }

    case "light":
    {
        var name = args[1];
        await using var c = await Connect(HttpTransportMode.StreamableHttp, "/api/mcp");
        async Task<string?> State() => Entities(Unwrap(string.Join("\n", (await c.CallToolAsync("homeassistant__GetLiveContext", new Dictionary<string, object?>())).Content.OfType<TextContentBlock>().Select(b => b.Text)))).FirstOrDefault(e => e.Name == name && e.Domain == "light")?.State;
        var before = await State();
        Console.WriteLine($"{name}: state before = {before}");
        if (before != "off") { Console.WriteLine("not off: nothing done (the spike only switches a light that is off, then back off)"); break; }
        var sw = Stopwatch.StartNew();
        var on = await c.CallToolAsync("intent__HassTurnOn", new Dictionary<string, object?> { ["name"] = name, ["domain"] = new[] { "light" } });
        Console.WriteLine($"HassTurnOn in {sw.ElapsedMilliseconds} ms, isError={on.IsError}: {First(string.Join(" ", on.Content.OfType<TextContentBlock>().Select(b => b.Text)), 300)}");
        await Task.Delay(3000);
        Console.WriteLine($"{name}: state after turn on = {await State()}");
        sw.Restart();
        var off = await c.CallToolAsync("intent__HassTurnOff", new Dictionary<string, object?> { ["name"] = name, ["domain"] = new[] { "light" } });
        Console.WriteLine($"HassTurnOff in {sw.ElapsedMilliseconds} ms, isError={off.IsError}: {First(string.Join(" ", off.Content.OfType<TextContentBlock>().Select(b => b.Text)), 300)}");
        await Task.Delay(3000); // the state reported by GetLiveContext can lag the action by a second or two
        var after = await State();
        Console.WriteLine($"{name}: state after turn off = {after} (restored: {after == before})");
        break;
    }

    case "reconnect":
    {
        var c = await Connect(HttpTransportMode.StreamableHttp, "/api/mcp");
        async Task<string> Call(McpClient client)
        {
            var sw = Stopwatch.StartNew();
            try { var r = await client.CallToolAsync("llm__GetDateTime", new Dictionary<string, object?>()); return $"OK in {sw.ElapsedMilliseconds} ms (isError={r.IsError})"; }
            catch (Exception ex) { return $"FAILED in {sw.ElapsedMilliseconds} ms: {ex.GetType().Name}: {First(ex.Message)}"; }
        }
        Console.WriteLine($"session id from the server: {(c.SessionId is null ? "none (stateless)" : "yes")}");
        Console.WriteLine($"1. before the cut: {await Call(c)}");
        cut.Broken = true;
        Console.WriteLine($"2. during the cut: {await Call(c)}");
        cut.Broken = false;
        Console.WriteLine($"3. after the cut, same client: {await Call(c)}");
        await c.DisposeAsync();
        var c2 = await Connect(HttpTransportMode.StreamableHttp, "/api/mcp");
        Console.WriteLine($"4. after the cut, new client: {await Call(c2)}");
        // A cut while connecting: creation fails, a retry succeeds.
        cut.Broken = true;
        try { await Connect(HttpTransportMode.StreamableHttp, "/api/mcp"); Console.WriteLine("5. connect during the cut: unexpectedly OK"); }
        catch (Exception ex) { Console.WriteLine($"5. connect during the cut: {ex.GetType().Name}: {First(ex.Message)}"); }
        cut.Broken = false;
        await using var c3 = await Connect(HttpTransportMode.StreamableHttp, "/api/mcp");
        Console.WriteLine($"6. connect after the cut: {await Call(c3)}");
        await c2.DisposeAsync();
        break;
    }
}

static string First(string? s, int max = 160) { s = (s ?? "").Replace("\n", " "); return s.Length > max ? s[..max] + "…" : s; }

// GetLiveContext returns {"success":true,"result":"Live Context: ...yaml..."} as text.
static string Unwrap(string body)
{
    try { using var d = JsonDocument.Parse(body); return d.RootElement.TryGetProperty("result", out var r) ? r.GetString() ?? body : body; }
    catch (JsonException) { return body; }
}

static List<Entity> Entities(string live)
{
    var list = new List<Entity>();
    foreach (var block in Regex.Split(live, @"^- ", RegexOptions.Multiline).Skip(1))
    {
        string? Field(string f) => Regex.Match(block, $@"^\s*{f}:\s*'?([^'\n]*)'?", RegexOptions.Multiline) is { Success: true } m ? m.Groups[1].Value.Trim() : null;
        list.Add(new Entity(Field("names") ?? "?", Field("domain") ?? "?", Field("state"), Field("areas"), Field("device_class")));
    }
    return list;
}

record Entity(string Name, string Domain, string? State, string? Area, string? DeviceClass);

/// <summary>Simulates a network cut: while Broken, every request fails like an unreachable host.</summary>
sealed class CuttableHandler : DelegatingHandler
{
    public volatile bool Broken;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Broken ? throw new HttpRequestException("simulated network cut") : base.SendAsync(request, ct);
}
