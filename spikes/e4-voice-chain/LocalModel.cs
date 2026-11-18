using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Spikes.VoiceChain;

/// <summary>Streams a chat answer from Ollama and returns as soon as the first full sentence is known.</summary>
public sealed class LocalModel(string baseUrl, string model)
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };
    public string Model => model;

    public async Task<(string FirstSentence, long FirstTokenMs, long FirstSentenceMs, string Full, long TotalMs)> AskAsync(string system, string question)
    {
        var sw = Stopwatch.StartNew();
        var body = new
        {
            model, stream = true, keep_alive = "30m", think = model.StartsWith("qwen3") ? (bool?)false : null,
            options = new { temperature = 0.3, num_ctx = 4096 },
            messages = new object[] { new { role = "system", content = system }, new { role = "user", content = question } },
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/chat") { Content = JsonContent.Create(body) };
        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        using var reader = new StreamReader(await resp.Content.ReadAsStreamAsync());
        var text = new StringBuilder();
        long firstToken = -1, firstSentence = -1; string? sentence = null;
        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.Length == 0) continue;
            using var d = JsonDocument.Parse(line);
            var chunk = d.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
            if (chunk.Length > 0 && firstToken < 0) firstToken = sw.ElapsedMilliseconds;
            text.Append(chunk);
            if (sentence is null && FirstSentence(text.ToString()) is { } s) { sentence = s; firstSentence = sw.ElapsedMilliseconds; }
            if (d.RootElement.GetProperty("done").GetBoolean()) break;
        }
        var full = text.ToString().Trim();
        return (sentence ?? full, firstToken, firstSentence < 0 ? sw.ElapsedMilliseconds : firstSentence, full, sw.ElapsedMilliseconds);
    }

    /// <summary>A sentence ends with . ! ? or … followed by whitespace (so "3.5" or "M." mid-stream do not cut).</summary>
    static string? FirstSentence(string s)
    {
        for (var i = 0; i < s.Length - 1; i++)
            if (".!?…".Contains(s[i]) && char.IsWhiteSpace(s[i + 1]) && i > 8) return s[..(i + 1)].Trim();
        return null;
    }
}
