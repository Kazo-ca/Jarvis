using System.Text.Json;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Kernel.Web;

namespace Kazo.Jarvis.Features.Tools.Web;

/// <summary>What the two web tools share: reading their arguments and the words of a failure.</summary>
internal static class WebToolSupport
{
    /// <summary>Results are data for a model: addresses and accents stay readable.</summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The text of a string argument, or <see langword="null"/> when it is absent or empty.</summary>
    internal static string? Text(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
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

    /// <summary>The address a model wrote; « example.com » is read as <c>https://example.com</c>.</summary>
    internal static Uri? Address(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var address) ? address : null;
    }

    /// <summary>One sentence for the model: what went wrong. Jarvis does not get around a block (ADR 0022, section 4).</summary>
    internal static ToolException Failed(WebReadException exception) => new(exception.Failure switch
    {
        WebReadFailure.Unavailable => "The browser of Jarvis is not available on this computer.",
        WebReadFailure.AddressRefused => "This address is refused: it is on this computer or on the local network.",
        WebReadFailure.Blocked => "The site asked to verify that a human is browsing, or refused the request. The user can open the browser of Jarvis and solve it.",
        WebReadFailure.TimedOut => "The page took too long to load.",
        WebReadFailure.NothingRead => "The page loaded but no text could be read on it.",
        WebReadFailure.KeyRejected => "The key of the search service is missing or was refused.",
        _ => "The site could not be reached.",
    }, exception);
}
