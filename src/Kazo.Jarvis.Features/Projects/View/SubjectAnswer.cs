using System.Globalization;
using System.Text.RegularExpressions;

namespace Kazo.Jarvis.Features.Projects.View;

/// <summary>
/// A message that answers a task of the subject, written as in a chat room: <c>T3&gt; oui, on fait comme ça</c>
/// (ADR 0026, section 4).
/// </summary>
/// <param name="Number">The number of the task in the subject.</param>
/// <param name="Text">The message, without its prefix.</param>
public sealed partial record SubjectAnswer(int Number, string Text)
{
    /// <summary>Reads the prefix of a message.</summary>
    /// <param name="message">What the user wrote.</param>
    /// <returns>The task and the message, or <see langword="null"/> when the message names no task or says nothing to it.</returns>
    public static SubjectAnswer? Parse(string? message)
    {
        var match = Prefix().Match(message ?? string.Empty);
        return match.Success && int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
            ? new SubjectAnswer(number, match.Groups["text"].Value.Trim())
            : null;
    }

    [GeneratedRegex(@"^\s*T\s?(?<number>\d{1,4})\s*>\s*(?<text>\S.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Prefix();
}
