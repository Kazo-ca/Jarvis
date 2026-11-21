using System.Text.Json;
using Kazo.Jarvis.Kernel.Files;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Features.Tools.Files;

/// <summary>What the two file tools share: reading their arguments, the class of a path, the words of a failure.</summary>
internal static class FileToolSupport
{
    /// <summary>Results are data for a model: paths and accents stay readable.</summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The text of a string argument, or <see langword="null"/> when it is absent or empty.</summary>
    internal static string? Text(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    /// <summary>A whole number argument, written as a number or as text; <paramref name="fallback"/> when absent.</summary>
    internal static int Integer(JsonElement arguments, string name, int fallback)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => fallback,
        };
    }

    /// <summary>
    /// The class of a read of <paramref name="path"/> (ADR 0017, section 3): « Lecture » inside an allowed folder,
    /// « Interdite » anywhere else, which the decision point denies and logs.
    /// </summary>
    internal static ActionClass ClassOf(CheckedPath path) =>
        path.Verdict == PathVerdict.Allowed ? ActionClass.Read : ActionClass.Forbidden;

    /// <summary>One sentence for the model: what went wrong with a folder or a file. Never the content of a file.</summary>
    internal static ToolException Failed(LocalFileException exception) => new(exception.Failure switch
    {
        LocalFileFailure.NotFound => "Nothing exists at this path.",
        LocalFileFailure.NotAFolder => "This path is a file, not a folder. Use read_file.",
        LocalFileFailure.NotAFile => "This path is a folder, not a file. Use list_directory.",
        LocalFileFailure.AccessDenied => "Windows refused the read of this path.",
        LocalFileFailure.NotText => "This file is not a text file.",
        LocalFileFailure.TooLarge => "This file is too large to be read.",
        _ => "This path could not be read.",
    }, exception);
}
