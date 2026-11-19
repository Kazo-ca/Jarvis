using System.Text.RegularExpressions;

namespace Kazo.Jarvis.Tests.Unit.Architecture;

/// <summary>
/// No style or script in Razor markup (ADR 0002, section 7; AGENTS.md, II.1): CSS and JavaScript of a
/// component live in <c>.razor.css</c> and <c>.razor.js</c> next to it.
/// </summary>
internal static partial class RazorMarkupRule
{
    /// <summary>
    /// Host documents (the <c>&lt;html&gt;</c> page of a host) that may load a script file with an
    /// empty <c>&lt;script src="…"&gt;&lt;/script&gt;</c> element: Blazor's own script must be loaded there.
    /// Inline script content and styles stay forbidden everywhere.
    /// </summary>
    public static readonly string[] HostDocuments = ["src/Kazo.Jarvis.Web/Components/App.razor"];

    /// <summary>The problems found in one Razor file, empty when it follows the rule.</summary>
    /// <param name="markup">The file content.</param>
    /// <param name="isHostDocument">True for a file listed in <see cref="HostDocuments"/>.</param>
    public static string[] Problems(string markup, bool isHostDocument)
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

        foreach (Match script in ScriptElement().Matches(code))
        {
            var isFileReference = script.Groups["attributes"].Value.Contains("src=", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(script.Groups["body"].Value);
            if (!isHostDocument || !isFileReference)
            {
                problems.Add("<script> element");
            }
        }

        return [.. problems.Distinct()];
    }

    /// <summary>Razor comments <c>@* … *@</c> and HTML comments, which may mention the forbidden tags.</summary>
    [GeneratedRegex(@"@\*.*?\*@|<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"<style\b", RegexOptions.IgnoreCase)]
    private static partial Regex StyleElement();

    [GeneratedRegex(@"(?<![\w-])style\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex StyleAttribute();

    [GeneratedRegex(@"<script\b(?<attributes>[^>]*)>(?<body>.*?)(</script\s*>|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptElement();
}
