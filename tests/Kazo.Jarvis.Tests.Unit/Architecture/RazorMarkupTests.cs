namespace Kazo.Jarvis.Tests.Unit.Architecture;

/// <summary>No <c>&lt;style&gt;</c>, <c>&lt;script&gt;</c> or <c>style="…"</c> in a Razor file (ADR 0002, section 7).</summary>
[TestFixture]
public sealed class RazorMarkupTests
{
    [Test]
    public void No_razor_file_holds_style_or_script()
    {
        var problems = RepositoryLayout.SourceFiles(RepositoryLayout.Src, "*.razor")
            .SelectMany(file =>
            {
                var relative = RepositoryLayout.Relative(file);
                var isHost = RazorMarkupRule.HostDocuments.Contains(relative);
                return RazorMarkupRule.Problems(File.ReadAllText(file), isHost).Select(problem => $"{relative}: {problem}");
            });

        Assert.That(problems, Is.Empty, "Move CSS to MyComponent.razor.css and JavaScript to MyComponent.razor.js.");
    }

    [Test]
    public void Every_listed_host_document_exists()
    {
        foreach (var document in RazorMarkupRule.HostDocuments)
        {
            Assert.That(File.Exists(Path.Combine(RepositoryLayout.Root, document)), Is.True, document);
        }
    }

    // The tests below prove the rule on samples.

    [TestCase("<div style=\"color: red\"></div>", "style=\"…\" attribute")]
    [TestCase("<div STYLE = 'x'></div>", "style=\"…\" attribute")]
    [TestCase("<style>.a { }</style>", "<style> element")]
    [TestCase("<script>alert(1)</script>", "<script> element")]
    [TestCase("<script src=\"a.js\"></script>", "<script> element")]
    public void Rule_rejects_styles_and_scripts_in_a_component(string markup, string expected)
    {
        Assert.That(RazorMarkupRule.Problems(markup, isHostDocument: false), Is.EqualTo(new[] { expected }));
    }

    [TestCase("<div class=\"orb\" data-style=\"x\"></div>")]
    [TestCase("@* no <style> nor <script> here *@<div></div>")]
    [TestCase("<!-- style=\"x\" --><div></div>")]
    public void Rule_accepts_clean_markup_and_comments(string markup)
    {
        Assert.That(RazorMarkupRule.Problems(markup, isHostDocument: false), Is.Empty);
    }

    [Test]
    public void Rule_lets_a_host_document_load_a_script_file_only()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RazorMarkupRule.Problems("<script src=\"_framework/blazor.web.js\"></script>", isHostDocument: true), Is.Empty);
            Assert.That(RazorMarkupRule.Problems("<script>start()</script>", isHostDocument: true), Is.EqualTo(new[] { "<script> element" }));
            Assert.That(RazorMarkupRule.Problems("<style>.a { }</style>", isHostDocument: true), Is.EqualTo(new[] { "<style> element" }));
        });
    }
}
