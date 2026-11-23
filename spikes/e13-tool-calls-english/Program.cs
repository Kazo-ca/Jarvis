// Spike E13: replay the E7 protocol with tool descriptions and requests in ENGLISH, and with the new
// hand_off_to_claude tool (kind: question|task, reason) in both languages.
// Three configurations: "en" (English system prompt, descriptions and requests), "fr" (all French),
// "mixed" (English descriptions, French system prompt and requests).
// Usage: dotnet run -c Release -- [model] [runs] [en|fr|mixed|all]
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

var baseUrl = Environment.GetEnvironmentVariable("JARVIS_OLLAMA") ?? "http://localhost:11435";
var model = args.Length > 0 ? args[0] : "qwen3:14b";
var runs = args.Length > 1 ? int.Parse(args[1]) : 3;
var which = args.Length > 2 ? args[2] : "all";
var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

const string SystemFr = """
Tu es Jarvis, le majordome du film : poli, concis, tu vouvoies et dis « Monsieur ».
Tu réponds dans la langue de la question. Tu disposes d'outils. Utilise un outil dès qu'il sert la demande.
Si la demande touche des fichiers, du code, une tâche à plusieurs étapes, ou dépasse tes connaissances, appelle hand_off_to_claude.
Pour une simple conversation, réponds sans outil.
""";
const string SystemEn = """
You are Jarvis, the butler from the film: polite, concise, you say "Sir".
You answer in the language of the question. You have tools. Use a tool whenever it serves the request.
If the request involves files, code, a multi-step task, or is beyond your knowledge, call hand_off_to_claude.
For plain conversation, answer without a tool.
""";

JsonObject Tool(string name, string description, params (string Name, string Type, string Desc, string[]? Enum)[] ps) => new()
{
    ["type"] = "function",
    ["function"] = new JsonObject
    {
        ["name"] = name, ["description"] = description,
        ["parameters"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(ps.Select(p =>
            {
                var o = new JsonObject { ["type"] = p.Type, ["description"] = p.Desc };
                if (p.Enum is not null) o["enum"] = new JsonArray(p.Enum.Select(e => (JsonNode?)JsonValue.Create(e)).ToArray());
                return KeyValuePair.Create(p.Name, (JsonNode?)o);
            })),
            ["required"] = new JsonArray(ps.Select(p => (JsonNode?)JsonValue.Create(p.Name)).ToArray()),
        },
    },
};

JsonArray ToolsFr() => new(
    Tool("get_datetime", "Date et heure actuelles."),
    Tool("calculate", "Évalue une expression arithmétique.", ("expression", "string", "ex. 12*7+3", null)),
    Tool("web_search", "Recherche sur le web (actualités, météo, faits récents).", ("query", "string", "requête", null)),
    Tool("home_assistant_turn_on", "Allume un appareil ou une pièce dans Home Assistant.", ("target", "string", "pièce ou entité, ex. salon", null)),
    Tool("home_assistant_turn_off", "Éteint un appareil ou une pièce dans Home Assistant.", ("target", "string", "pièce ou entité", null)),
    Tool("read_file", "Lit un fichier texte local.", ("path", "string", "chemin", null)),
    Tool("hand_off_to_claude", "Passe la main à Claude. kind=question pour une question difficile ou qui demande des connaissances ; kind=task pour une tâche : code, fichiers, plusieurs étapes.",
        ("kind", "string", "question ou task", ["question", "task"]), ("reason", "string", "motif court", null)));

JsonArray ToolsEn() => new(
    Tool("get_datetime", "Current date and time."),
    Tool("calculate", "Evaluates an arithmetic expression.", ("expression", "string", "e.g. 12*7+3", null)),
    Tool("web_search", "Searches the web (news, weather, recent facts).", ("query", "string", "query", null)),
    Tool("home_assistant_turn_on", "Turns on a device or a room in Home Assistant.", ("target", "string", "room or entity, e.g. living room", null)),
    Tool("home_assistant_turn_off", "Turns off a device or a room in Home Assistant.", ("target", "string", "room or entity", null)),
    Tool("read_file", "Reads a local text file.", ("path", "string", "path", null)),
    Tool("hand_off_to_claude", "Hands off to Claude. kind=question for a hard question or one that needs knowledge; kind=task for a task: code, files, several steps.",
        ("kind", "string", "question or task", ["question", "task"]), ("reason", "string", "short reason", null)));

// (prompt, expected tool, expected kind for hand_off_to_claude)
(string, string?, string?)[] casesFr =
[
    ("Quelle heure est-il ?", "get_datetime", null),
    ("Combien font 347 fois 29 ?", "calculate", null),
    ("Allume le salon.", "home_assistant_turn_on", null),
    ("Éteins les lumières de la cuisine, s'il te plaît.", "home_assistant_turn_off", null),
    ("Quel temps fera-t-il demain à Québec ?", "web_search", null),
    ("Lis-moi le fichier C:\\notes\\courses.txt.", "read_file", null),
    ("Corrige le bogue de compilation dans mon projet Kazo Bar et ouvre une PR.", "hand_off_to_claude", "task"),
    ("Écris un script PowerShell qui renomme toutes mes photos par date, puis teste-le.", "hand_off_to_claude", "task"),
    ("Demande à Claude de résumer les changements de la semaine dans le dépôt Kazo Os.", "hand_off_to_claude", "task"),
    ("Explique-moi en détail la différence entre la relativité restreinte et la relativité générale.", "hand_off_to_claude", "question"),
    ("Demande à Claude quelle architecture choisir pour une application mobile hors ligne.", "hand_off_to_claude", "question"),
    ("Merci Jarvis, ce sera tout.", null, null),
    ("Comment vas-tu ce matin ?", null, null),
];
(string, string?, string?)[] casesEn =
[
    ("What time is it?", "get_datetime", null),
    ("What is 347 times 29?", "calculate", null),
    ("Turn on the living room.", "home_assistant_turn_on", null),
    ("Turn off the kitchen lights, please.", "home_assistant_turn_off", null),
    ("What will the weather be like tomorrow in Quebec City?", "web_search", null),
    ("Read me the file C:\\notes\\groceries.txt.", "read_file", null),
    ("Fix the build error in my Kazo Bar project and open a PR.", "hand_off_to_claude", "task"),
    ("Write a PowerShell script that renames all my photos by date, then test it.", "hand_off_to_claude", "task"),
    ("Ask Claude to summarize this week's changes in the Kazo Os repository.", "hand_off_to_claude", "task"),
    ("Explain in detail the difference between special and general relativity.", "hand_off_to_claude", "question"),
    ("Ask Claude which architecture to choose for an offline mobile app.", "hand_off_to_claude", "question"),
    ("Thank you Jarvis, that will be all.", null, null),
    ("How are you this morning?", null, null),
];

var configs = new List<(string Name, string System, Func<JsonArray> Tools, (string, string?, string?)[] Cases)>
{
    ("en", SystemEn, ToolsEn, casesEn),
    ("fr", SystemFr, ToolsFr, casesFr),
    ("mixed", SystemFr, ToolsEn, casesFr),
};

// Warm the model so the first measured call does not include loading.
await Chat(SystemEn, ToolsEn(), "Hello");

foreach (var cfg in configs.Where(c => which == "all" || c.Name == which))
{
    var ok = 0; var total = 0; var official = 0; var textCalls = 0; var kindOk = 0; var kindTotal = 0; var emptyArgs = 0;
    var latencies = new List<long>(); var lines = new List<string>();
    foreach (var (prompt, expected, kind) in cfg.Cases)
    {
        var got = new List<string>();
        for (var r = 0; r < runs; r++)
        {
            var sw = Stopwatch.StartNew();
            var msg = await Chat(cfg.System, cfg.Tools(), prompt);
            latencies.Add(sw.ElapsedMilliseconds);
            string? called = null; JsonObject? callArgs = null;
            if (msg["tool_calls"]?.AsArray() is { Count: > 0 } calls)
            {
                called = calls[0]!["function"]!["name"]!.GetValue<string>();
                callArgs = calls[0]!["function"]!["arguments"] as JsonObject;
                official++;
            }
            else if (TextCall(msg["content"]?.GetValue<string>()) is { } tc) { called = tc.Name; callArgs = tc.Args; textCalls++; }
            if (callArgs is { Count: 0 } && called is not null and not "get_datetime") emptyArgs++;
            var gotKind = callArgs?["kind"]?.ToString();
            if (kind is not null && called == expected) { kindTotal++; if (gotKind == kind) kindOk++; }
            total++; if (called == expected) ok++;
            got.Add(called is null ? "-" : called == "hand_off_to_claude" ? $"hand_off({gotKind}: {Trunc(callArgs?["reason"]?.ToString())})" : $"{called}({Trunc(callArgs?.ToJsonString())})");
        }
        var allRight = got.All(g => expected is null ? g == "-" : g.StartsWith(expected == "hand_off_to_claude" ? "hand_off(" : expected + "("));
        lines.Add($"{(allRight ? "OK" : "KO")}  {prompt}\n      expected {expected ?? "-"}{(kind is null ? "" : $" ({kind})")}\n      got {string.Join(" | ", got)}");
    }
    latencies.Sort();
    Console.WriteLine($"\n===== {cfg.Name} =====");
    lines.ForEach(Console.WriteLine);
    Console.WriteLine($"SUMMARY {cfg.Name} {model}: right tool {ok}/{total} ({100.0 * ok / total:F0} %), official channel {official}, JSON text {textCalls}, kind right {kindOk}/{kindTotal}, empty args {emptyArgs}, mean {latencies.Average():F0} ms, median {latencies[latencies.Count / 2]} ms, p90 {latencies[(int)(latencies.Count * 0.9)]} ms");
}

async Task<JsonNode> Chat(string system, JsonArray tools, string prompt)
{
    var body = new JsonObject
    {
        ["model"] = model, ["stream"] = false, ["keep_alive"] = "30m",
        ["think"] = model.StartsWith("qwen3") ? false : null,
        ["options"] = new JsonObject { ["temperature"] = 0.2, ["num_ctx"] = 4096 },
        ["tools"] = tools,
        ["messages"] = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = system }, new JsonObject { ["role"] = "user", ["content"] = prompt }),
    };
    var resp = await http.PostAsJsonAsync($"{baseUrl}/api/chat", body);
    resp.EnsureSuccessStatusCode();
    return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!["message"]!;
}

static string Trunc(string? s) => s is null ? "" : s.Length > 60 ? s[..60] + "…" : s;

/// <summary>Lenient parser from E7: accept a reply that is one whole JSON object naming a tool.</summary>
static (string Name, JsonObject? Args)? TextCall(string? content)
{
    if (string.IsNullOrWhiteSpace(content)) return null;
    var t = content.Trim().Trim('`');
    if (t.StartsWith("json")) t = t[4..];
    try
    {
        return JsonNode.Parse(t.Trim()) is JsonObject o && o["name"] is JsonValue v
            ? (v.GetValue<string>(), (o["arguments"] ?? o["parameters"]) as JsonObject) : null;
    }
    catch (JsonException) { return null; }
}
