using Kazo.Jarvis.Features.Conversation.Dialogue;
using Kazo.Jarvis.Features.Tools.Calculator;
using Kazo.Jarvis.Features.Tools.Catalog;
using Kazo.Jarvis.Features.Tools.Clock;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Tests.Unit.Features.Tools;

/// <summary>What the tools page computes (ADR 0017, section 4), and the pages the conversation lists for a call (ADR 0022, section 1).</summary>
[TestFixture]
public sealed class ToolCatalogTests
{
    [Test]
    public void The_size_of_the_list_grows_with_the_tools_given_to_the_model()
    {
        ITool clock = new ClockTool(TimeProvider.System);
        ITool calculator = new CalculatorTool();

        var one = ToolCatalog.EstimateTokens([clock], AssistantLanguage.French);
        var two = ToolCatalog.EstimateTokens([clock, calculator], AssistantLanguage.French);

        Assert.Multiple(() =>
        {
            Assert.That(ToolCatalog.EstimateTokens([], AssistantLanguage.French), Is.Zero);
            Assert.That(one, Is.InRange(20, 200), "A name, a sentence and a small schema.");
            Assert.That(two, Is.GreaterThan(one));
        });
    }

    [Test]
    public void The_pages_of_a_result_are_its_web_addresses_each_once()
    {
        const string result = """
            {"query":"météo","results":[{"title":"A","url":"https://a.example/page?x=1"},{"title":"B","url":"http://b.example/"},
            {"title":"A again","url":"https://a.example/page?x=1"}],"note":"see https://c.example/end. file:///C:/x ftp://d.example"}
            """;

        Assert.That(ToolResultPages.In(result).Select(page => page.AbsoluteUri),
            Is.EqualTo(new[] { "https://a.example/page?x=1", "http://b.example/", "https://c.example/end" }));
    }

    [Test]
    public void A_result_without_address_lists_no_page()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToolResultPages.In(null), Is.Empty);
            Assert.That(ToolResultPages.In("87"), Is.Empty);
            Assert.That(ToolResultPages.In(string.Concat(Enumerable.Range(1, 30).Select(index => $"https://e.example/{index} "))), Has.Count.EqualTo(ToolResultPages.Most));
        });
    }
}
