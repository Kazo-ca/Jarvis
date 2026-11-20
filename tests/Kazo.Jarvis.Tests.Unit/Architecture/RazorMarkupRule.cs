using System.Text.RegularExpressions;

namespace Kazo.Jarvis.Tests.Unit.Architecture;

/// <summary>
/// No style in Razor markup (ADR 0002, section 7, as changed by ADR 0012): the CSS of a component lives
/// in <c>.razor.css</c> next to it. Script is allowed in markup (ADR 0012).
/// </summary>
internal static partial class RazorMarkupRule
{
    /// <summary>The problems found in one Razor file, empty when it follows the rule.</summary>
    /// <param name="markup">The file content.</param>
    public static string[] Problems(string markup)
    {
        var code = Comments().Replace(markup, string.Empty);
        var problems = new List<string>();

        if (StyleElement().IsMatch(code))
        {
            problems.Add("<style> element");
        }

        if (StyleAttribute().IsMatch(code))
        {
            problems.Add("style=\"…\" attribute");
        }

        return [.. problems];
    }

    /// <summary>Razor comments <c>@* … *@</c> and HTML comments, which may mention the forbidden tags.</summary>
    [GeneratedRegex(@"@\*.*?\*@|<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"<style\b", RegexOptions.IgnoreCase)]
    private static partial Regex StyleElement();

    [GeneratedRegex(@"(?<![\w-])style\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex StyleAttribute();
}
