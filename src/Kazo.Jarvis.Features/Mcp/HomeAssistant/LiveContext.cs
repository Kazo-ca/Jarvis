using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kazo.Jarvis.Features.Mcp.HomeAssistant;

/// <summary>An entity Home Assistant exposes, as <c>GetLiveContext</c> shows it (spike E14).</summary>
/// <param name="Names">Its name and its aliases.</param>
/// <param name="Domain">Its domain: <c>light</c>, <c>lock</c>, <c>cover</c>…</param>
/// <param name="Areas">The rooms it is in.</param>
/// <param name="DeviceClass">Its device class, when it has one: <c>garage</c>, <c>door</c>…</param>
public sealed record HomeEntity(IReadOnlyList<string> Names, string Domain, IReadOnlyList<string> Areas, string? DeviceClass)
{
    /// <summary>Whether one of its names is <paramref name="name"/>, whatever the case.</summary>
    /// <param name="name">A name.</param>
    /// <returns><see langword="true"/> on an exact match.</returns>
    public bool IsNamed(string name) => Names.Any(own => string.Equals(own, name.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Reads the answer of <c>GetLiveContext</c> (spike E14): a JSON object whose <c>result</c> is a text, a list in
/// YAML with one entry per exposed entity (<c>names</c>, <c>domain</c>, <c>state</c>, <c>areas</c>,
/// <c>attributes</c>). Only the lines the classification needs are read; the rest of the format may change.
/// </summary>
public static partial class LiveContext
{
    /// <summary>The exposed entities of an answer.</summary>
    /// <param name="answer">The text the tool returned.</param>
    /// <returns>The entities that have a domain. An entry without one is left out: it cannot be classed.</returns>
    public static IReadOnlyList<HomeEntity> Parse(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var live = Unwrap(answer);
        var entities = new List<HomeEntity>();
        foreach (var block in EntryStart().Split(live).Skip(1))
        {
            if (Field(block, "domain") is not { Length: > 0 } domain)
            {
                continue;
            }

            entities.Add(new HomeEntity(List(Field(block, "names")), domain.ToLowerInvariant(), List(Field(block, "areas")), Field(block, "device_class")?.ToLowerInvariant()));
        }

        return entities;
    }

    private static string Unwrap(string answer)
    {
        try
        {
            using var document = JsonDocument.Parse(answer);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.String
                ? result.GetString() ?? answer
                : answer;
        }
        catch (JsonException)
        {
            return answer;
        }
    }

    private static string? Field(string block, string name)
    {
        var match = Regex.Match(block, $@"^\s*{name}:[ \t]*(.*)$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups[1].Value.Trim();
        if (value.Length >= 2 && (value[0] == '\'' || value[0] == '"') && value[^1] == value[0])
        {
            value = value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return value.Length == 0 ? null : value;
    }

    /// <summary>Home Assistant joins the names, and the rooms, with a comma.</summary>
    private static IReadOnlyList<string> List(string? value) =>
        value is null ? [] : [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    [GeneratedRegex(@"^- ", RegexOptions.Multiline)]
    private static partial Regex EntryStart();
}
