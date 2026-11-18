// Spike E9 plan B: receive Home Assistant events in C# through its WebSocket API (MCP has no notifications).
// Read-only: subscribes, listens, never calls a service. Reads host and token from a local file outside the repo:
//   Host: https://example/   Token: `...`   (default C:\jarvis\temp\home-assistant.md)
// Usage: dotnet run -c Release -- [seconds] [file]
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

var seconds = args.Length > 0 ? int.Parse(args[0]) : 40;
var file = args.Length > 1 ? args[1] : @"C:\jarvis\temp\home-assistant.md";
var text = File.ReadAllText(file);
var host = Regex.Match(text, @"Host:\s*(\S+)").Groups[1].Value.TrimEnd('/');
var token = Regex.Match(text, @"Token:\s*`?([^`\s]+)").Groups[1].Value;
var wsUrl = new Uri(host.Replace("https://", "wss://").Replace("http://", "ws://") + "/api/websocket");

using var ws = new ClientWebSocket();
await ws.ConnectAsync(wsUrl, CancellationToken.None);
var id = 0;
Console.WriteLine($"connected to {wsUrl.Host}, first message: {(await Receive())?["type"]}");
await Send(new JsonObject { ["type"] = "auth", ["access_token"] = token });
var auth = await Receive();
Console.WriteLine($"auth: {auth?["type"]} (Home Assistant {auth?["ha_version"]})");
if (auth?["type"]?.GetValue<string>() != "auth_ok") return;

// 1) Every state change, as the generic event feed.
var eventsSub = ++id;
await Send(new JsonObject { ["id"] = eventsSub, ["type"] = "subscribe_events", ["event_type"] = "state_changed" });
// 2) An automation-style trigger evaluated by Home Assistant itself (time pattern: fires every 10 s, touches nothing).
var triggerSub = ++id;
await Send(new JsonObject
{
    ["id"] = triggerSub, ["type"] = "subscribe_trigger",
    ["trigger"] = new JsonObject { ["platform"] = "time_pattern", ["seconds"] = "/10" },
});

var stateEvents = 0; var triggers = 0; var lags = new List<double>(); var entities = new Dictionary<string, int>();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
try
{
    while (!cts.IsCancellationRequested && await Receive(cts.Token) is { } msg)
    {
        var type = msg["type"]?.GetValue<string>();
        if (type == "result") { Console.WriteLine($"subscription {msg["id"]}: success={msg["success"]} {msg["error"]?.ToJsonString()}"); continue; }
        if (type != "event") continue;
        var subId = msg["id"]!.GetValue<int>();
        var ev = msg["event"]!;
        if (subId == eventsSub)
        {
            stateEvents++;
            var entity = ev["data"]?["entity_id"]?.GetValue<string>() ?? "?";
            var domain = entity.Split('.')[0];
            entities[domain] = entities.GetValueOrDefault(domain) + 1;
            if (DateTimeOffset.TryParse(ev["time_fired"]?.GetValue<string>(), out var fired))
                lags.Add((DateTimeOffset.UtcNow - fired).TotalMilliseconds);
        }
        else if (subId == triggerSub)
        {
            triggers++;
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} trigger fired: {ev["variables"]?["trigger"]?["description"]}");
        }
    }
}
catch (OperationCanceledException) { }

lags.Sort();
Console.WriteLine($"{seconds} s: {stateEvents} state_changed events ({string.Join(", ", entities.OrderByDescending(e => e.Value).Take(8).Select(e => $"{e.Key} {e.Value}"))}), {triggers} triggers");
if (lags.Count > 0)
    Console.WriteLine($"delay time_fired -> received: median {lags[lags.Count / 2]:F0} ms, p90 {lags[(int)(lags.Count * 0.9)]:F0} ms (includes clock difference between the PC and Home Assistant)");

async Task Send(JsonObject o) => await ws.SendAsync(Encoding.UTF8.GetBytes(o.ToJsonString()), WebSocketMessageType.Text, true, CancellationToken.None);

async Task<JsonNode?> Receive(CancellationToken ct = default)
{
    var buf = new byte[64 * 1024]; using var ms = new MemoryStream();
    WebSocketReceiveResult r;
    do { r = await ws.ReceiveAsync(buf, ct); if (r.MessageType == WebSocketMessageType.Close) return null; ms.Write(buf, 0, r.Count); }
    while (!r.EndOfMessage);
    return JsonNode.Parse(ms.ToArray());
}
