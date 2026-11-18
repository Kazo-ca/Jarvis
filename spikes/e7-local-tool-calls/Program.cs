// Spike E7: does the local model call tools reliably in French (and English), including "hand off to Claude"?
// Each case is a spoken-style request with the tool we expect (or none). Uses Ollama's native tool calling.
// Usage: dotnet run -c Release -- [model] [runs]
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

var baseUrl = Environment.GetEnvironmentVariable("JARVIS_OLLAMA") ?? "http://localhost:11435";
var model = args.Length > 0 ? args[0] : "local-ia-active:latest";
var runs = args.Length > 1 ? int.Parse(args[1]) : 3;
var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

const string System = """
Tu es Jarvis, le majordome du film : poli, concis, tu vouvoies et dis « Monsieur ».
Tu réponds dans la langue de la question. Tu disposes d'outils. Utilise un outil dès qu'il sert la demande.
Si la demande touche des fichiers, du code, une tâche à plusieurs étapes, ou dépasse tes connaissances, appelle hand_off_to_claude avec un motif.
Pour une simple conversation, réponds sans outil.
""";

JsonObject Tool(string name, string description, params (string Name, string Type, string Desc)[] ps) => new()
{
    ["type"] = "function",
    ["function"] = new JsonObject
    {
        ["name"] = name, ["description"] = description,
        ["parameters"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(ps.Select(p => KeyValuePair.Create(p.Name, (JsonNode?)new JsonObject { ["type"] = p.Type, ["description"] = p.Desc }))),
            ["required"] = new JsonArray(ps.Select(p => (JsonNode?)JsonValue.Create(p.Name)).ToArray()),
        },
    },
};

var tools = new JsonArray(
    Tool("get_datetime", "Date et heure actuelles."),
    Tool("calculate", "Évalue une expression arithmétique.", ("expression", "string", "ex. 12*7+3")),
    Tool("web_search", "Recherche sur le web (actualités, météo, faits récents).", ("query", "string", "requête")),
    Tool("home_assistant_turn_on", "Allume un appareil ou une pièce dans Home Assistant.", ("target", "string", "pièce ou entité, ex. salon")),
    Tool("home_assistant_turn_off", "Éteint un appareil ou une pièce dans Home Assistant.", ("target", "string", "pièce ou entité")),
    Tool("read_file", "Lit un fichier texte local.", ("path", "string", "chemin")),
    Tool("hand_off_to_claude", "Passe la main à Claude pour une tâche trop complexe : code, fichiers, tâche à étapes, question difficile.", ("reason", "string", "motif court"), ("request", "string", "la demande à transmettre")));

(string Prompt, string? Expected)[] cases =
[
    ("Quelle heure est-il ?", "get_datetime"),
    ("Combien font 347 fois 29 ?", "calculate"),
    ("Allume le salon.", "home_assistant_turn_on"),
    ("Éteins les lumières de la cuisine, s'il te plaît.", "home_assistant_turn_off"),
    ("Quel temps fera-t-il demain à Québec ?", "web_search"),
    ("Lis-moi le fichier C:\\notes\\courses.txt.", "read_file"),
    ("Corrige le bogue de compilation dans mon projet Kazo Bar et ouvre une PR.", "hand_off_to_claude"),
    ("Écris un script PowerShell qui renomme toutes mes photos par date, puis teste-le.", "hand_off_to_claude"),
    ("Demande à Claude de résumer les changements de la semaine dans le dépôt Kazo Os.", "hand_off_to_claude"),
    ("Merci Jarvis, ce sera tout.", null),
    ("Comment vas-tu ce matin ?", null),
    ("Turn on the living room.", "home_assistant_turn_on"),
    ("What time is it?", "get_datetime"),
    ("Refactor my C# solution to use vertical slices and run the tests.", "hand_off_to_claude"),
];

var ok = 0; var total = 0; var textCalls = 0; var latencies = new List<long>(); var badArgs = 0;
var perCase = new List<string>();
foreach (var (prompt, expected) in cases)
{
    var results = new List<string>();
    for (var r = 0; r < runs; r++)
    {
        var body = new JsonObject
        {
            ["model"] = model, ["stream"] = false, ["keep_alive"] = "30m",
            // Thinking models (qwen3) reason before answering; a voice assistant cannot wait for that.
            ["think"] = model.StartsWith("qwen3") ? false : null,
            ["options"] = new JsonObject { ["temperature"] = 0.2, ["num_ctx"] = 4096 },
            ["tools"] = tools.DeepClone(),
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = System }, new JsonObject { ["role"] = "user", ["content"] = prompt }),
        };
        var sw = Stopwatch.StartNew();
        var resp = await http.PostAsJsonAsync($"{baseUrl}/api/chat", body);
        var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        latencies.Add(sw.ElapsedMilliseconds);
        var msg = json["message"]!;
        var calls = msg["tool_calls"]?.AsArray();
        var called = calls is { Count: > 0 } ? calls[0]!["function"]!["name"]!.GetValue<string>() : null;
        // Some models write the call as JSON text instead of using the tool channel: count that as a failure.
        if (called is null && TextCall(msg["content"]?.GetValue<string>()) is { } textName) { called = textName; textCalls++; }
        if (calls is { Count: > 0 } && calls[0]!["function"]!["arguments"] is JsonObject a && expected is not null
            && a.Count == 0 && expected is not "get_datetime") badArgs++;
        total++; if (called == expected) ok++;
        results.Add(called ?? "-");
    }
    perCase.Add($"{(results.All(x => x == (expected ?? "-")) ? "OK " : "KO ")} {prompt,-80} expected {expected ?? "-",-24} got {string.Join(", ", results)}");
}
perCase.ForEach(Console.WriteLine);
latencies.Sort();
Console.WriteLine($"\n{model}: {ok}/{total} correct ({100.0 * ok / total:F0} %), {textCalls} of them written as JSON text instead of the tool channel (rescued by a lenient parser), {badArgs} calls with empty arguments, median {latencies[latencies.Count / 2]} ms, p90 {latencies[(int)(latencies.Count * 0.9)]} ms");

/// <summary>Lenient parser: some models (qwen2.5-coder) write {"name":..,"arguments":..} as plain text.
/// Accept it only when the whole reply is one JSON object naming a declared tool.</summary>
static string? TextCall(string? content)
{
    if (string.IsNullOrWhiteSpace(content)) return null;
    var t = content.Trim().Trim('`');
    if (t.StartsWith("json")) t = t[4..];
    try { return JsonNode.Parse(t.Trim()) is JsonObject o && o["name"] is JsonValue v ? v.GetValue<string>() : null; }
    catch (JsonException) { return null; }
}
