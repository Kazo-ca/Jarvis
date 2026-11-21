using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Spikes.WebViewSearch;

/// <summary>All folders the spike writes to; everything lives under one root outside the repo.</summary>
internal sealed record Paths(string Root)
{
    public string Profile => Path.Combine(Root, "profile");
    public string Out => Path.Combine(Root, "out");
    public string Pages => Path.Combine(Root, "pages");
    public string Downloads => Path.Combine(Root, "downloads");

    public void Ensure()
    {
        foreach (var d in new[] { Profile, Out, Pages, Downloads }) Directory.CreateDirectory(d);
    }
}

internal static class Log
{
    private static string? file;

    public static void Init(Paths paths, string mode) => file = Path.Combine(paths.Out, $"{mode}.log");

    public static void Line(string text)
    {
        if (file is null) return;
        File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff} {text}{Environment.NewLine}");
    }
}

internal static class Report
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Write(Paths paths, string name, object data)
    {
        File.WriteAllText(Path.Combine(paths.Out, name + ".json"), JsonSerializer.Serialize(data, Options));
    }

    public static double Median(IEnumerable<double> values)
    {
        var v = values.OrderBy(x => x).ToArray();
        if (v.Length == 0) return 0;
        return v.Length % 2 == 1 ? v[v.Length / 2] : Math.Round((v[v.Length / 2 - 1] + v[v.Length / 2]) / 2);
    }
}
