namespace Kazo.Jarvis.Tests.Unit.Architecture;

/// <summary>
/// No <c>&lt;style&gt;</c> or <c>style="…"</c> in a Razor file (ADR 0002, section 7). Script is allowed
/// in markup since ADR 0012.
/// </summary>
[TestFixture]
public sealed class RazorMarkupTests
{
    [Test]
    public void No_razor_file_holds_style()
    {
        var problems = RepositoryLayout.SourceFiles(RepositoryLayout.Src, "*.razor")
            .SelectMany(file => RazorMarkupRule.Problems(File.ReadAllText(file))
                .Select(problem => $"{RepositoryLayout.Relative(file)}: {problem}"));

        Assert.That(problems, Is.Empty, "Move CSS to MyComponent.razor.css.");
    }

    // The tests below prove the rule on samples.

    [TestCase("<div style=\"color: red\"></div>", "style=\"…\" attribute")]
    [TestCase("<div STYLE = 'x'></div>", "style=\"…\" attribute")]
    [TestCase("<style>.a { }</style>", "<style> element")]
    public void Rule_rejects_styles_in_markup(string markup, string expected)
    {
        Assert.That(RazorMarkupRule.Problems(markup), Is.EqualTo(new[] { expected }));
    }

    [TestCase("<div class=\"orb\" data-style=\"x\"></div>")]
    [TestCase("@* no <style> here *@<div></div>")]
    [TestCase("<!-- style=\"x\" --><div></div>")]
    [TestCase("<script src=\"_framework/blazor.web.js\"></script>")]
    [TestCase("<script>document.getElementById('orb').classList.add('on');</script>")]
    public void Rule_accepts_clean_markup_comments_and_scripts(string markup)
    {
        Assert.That(RazorMarkupRule.Problems(markup), Is.Empty);
    }
}
