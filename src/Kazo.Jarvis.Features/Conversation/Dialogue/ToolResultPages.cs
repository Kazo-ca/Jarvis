using System.Text.RegularExpressions;

namespace Kazo.Jarvis.Features.Conversation.Dialogue;

/// <summary>
/// The web pages named in the result of a tool call, so the conversation can list them with a « Voir la page »
/// button (ADR 0022, section 1). It reads addresses in the text of the result and knows no tool.
/// </summary>
public static partial class ToolResultPages
{
    /// <summary>The most pages listed for one call.</summary>
    public const int Most = 10;

    /// <summary>The <c>http</c> and <c>https</c> addresses written in <paramref name="result"/>, in order, each once.</summary>
    /// <param name="result">The result of a tool call, as returned to the model.</param>
    /// <returns>At most <see cref="Most"/> addresses.</returns>
    public static IReadOnlyList<Uri> In(string? result)
    {
        if (string.IsNullOrEmpty(result))
        {
            return [];
        }

        var pages = new List<Uri>();
        foreach (Match match in Address().Matches(result))
        {
            // A sentence may end right after an address.
            var text = match.Value.TrimEnd('.', ',', ';', ')', '…');
            if (Uri.TryCreate(text, UriKind.Absolute, out var address) && !pages.Contains(address))
            {
                pages.Add(address);
                if (pages.Count == Most)
                {
                    break;
                }
            }
        }

        return pages;
    }

    [GeneratedRegex(@"https?://[^\s""'\\<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex Address();
}
