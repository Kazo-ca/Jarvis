using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kazo.Jarvis.Adapters.Files;

/// <summary>JSON options shared by the settings file, the document store and the secrets file.</summary>
internal static class JarvisJson
{
    /// <summary>camelCase names, enums as text, indented, read case-insensitively; comments and trailing commas tolerated on read.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The same options, compact: for documents stored in the database.</summary>
    public static JsonSerializerOptions Compact { get; } = new(Options) { WriteIndented = false };

    /// <summary>Options to parse a whole file as a node.</summary>
    public static JsonDocumentOptions DocumentOptions { get; } = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
