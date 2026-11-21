using System.Globalization;
using System.Text.Json;
using Kazo.Jarvis.Features.Tools.Calculator;
using Kazo.Jarvis.Features.Tools.Clock;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Permissions;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Tests.Unit.Features.Tools;

/// <summary>The first two tools (ADR 0017, section 3): date and time, calculation. Both are reads, enabled by default.</summary>
[TestFixture]
public sealed class BuiltInToolsTests
{
    private static readonly JsonElement NoArguments = JsonDocument.Parse("{}").RootElement.Clone();

    private static JsonElement Expression(string expression) => JsonSerializer.SerializeToElement(new { expression });

    private static IEnumerable<ITool> Tools() => [new ClockTool(TimeProvider.System), new CalculatorTool()];

    [TestCaseSource(nameof(Tools))]
    public void A_tool_declares_its_name_its_class_and_a_description_in_both_languages(ITool tool)
    {
        Assert.Multiple(() =>
        {
            Assert.That(tool.Name, Does.Match("^[a-z][a-z0-9_]*$"), "English, ASCII, lower case and underscores (ADR 0007, section 3).");
            Assert.That(tool.Origin, Is.EqualTo(ToolOrigins.BuiltIn));
            Assert.That(tool.DeclaredClass, Is.EqualTo(ActionClass.Read));
            Assert.That(tool.Classify(NoArguments), Is.EqualTo(ActionClass.Read));
            Assert.That(tool.EnabledByDefault, Is.True, "Nothing leaves the machine.");
            Assert.That(tool.Schema.GetProperty("type").GetString(), Is.EqualTo("object"));
            Assert.That(tool.Describe(AssistantLanguage.French), Is.Not.Empty);
            Assert.That(tool.Describe(AssistantLanguage.English), Is.Not.Empty.And.Not.EqualTo(tool.Describe(AssistantLanguage.French)));
        });
    }

    [Test]
    public async Task The_clock_gives_the_local_date_time_and_zone_as_data()
    {
        var time = new FixedTime(
            new DateTimeOffset(2026, 11, 21, 19, 5, 9, TimeSpan.Zero),
            TimeZoneInfo.CreateCustomTimeZone("Test/Toronto", TimeSpan.FromHours(-5), "Toronto", "Toronto"));

        using var result = JsonDocument.Parse(await new ClockTool(time).InvokeAsync(NoArguments, CancellationToken.None));

        var root = result.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("date").GetString(), Is.EqualTo("2026-11-21"));
            Assert.That(root.GetProperty("time").GetString(), Is.EqualTo("14:05:09"));
            Assert.That(root.GetProperty("weekday").GetString(), Is.EqualTo("Saturday"));
            Assert.That(root.GetProperty("time_zone").GetString(), Is.EqualTo("Test/Toronto"));
            Assert.That(root.GetProperty("utc_offset").GetString(), Is.EqualTo("-05:00"));
        });
    }

    [TestCase("12*7+3", "87")]
    [TestCase("0.1 + 0.2", "0.3")]
    [TestCase("2 + 3 * 4", "14")]
    [TestCase("(2 + 3) * 4", "20")]
    [TestCase("10 / 4", "2.5")]
    [TestCase("1/3", "0.333333333333")]
    [TestCase("17 % 5", "2")]
    [TestCase("2^10", "1024")]
    [TestCase("2**3", "8")]
    [TestCase("2^3^2", "512")]
    [TestCase("-2^2", "-4")]
    [TestCase("2^-1", "0.5")]
    [TestCase("--3", "3")]
    [TestCase("sqrt(16) + abs(-2)", "6")]
    [TestCase("sqrt(2)^2", "2")]
    [TestCase("round(2.5) + floor(2.9) + ceil(2.1)", "8")]
    [TestCase("3,5 × 2", "7")]
    [TestCase("9 ÷ 3 − 1", "2")]
    [TestCase("1e3 + 1", "1001")]
    [TestCase("2 * pi", "6.28318530718")]
    [TestCase("1234567.891 * 1000", "1234567891")]
    public async Task The_calculator_computes(string expression, string expected)
    {
        var result = await new CalculatorTool().InvokeAsync(Expression(expression), CancellationToken.None);

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("2 +")]
    [TestCase("(2 + 3")]
    [TestCase("2 3")]
    [TestCase("1 / 0")]
    [TestCase("sqrt(-1)")]
    [TestCase("9^9^9")]
    [TestCase("79228162514264337593543950335 * 10")]
    [TestCase("system('calc')")]
    [TestCase("x + 1")]
    [TestCase("1; DROP TABLE turns")]
    [TestCase("1.2.3")]
    public void The_calculator_refuses_what_it_does_not_understand(string expression)
    {
        Assert.That(
            async () => await new CalculatorTool().InvokeAsync(Expression(expression), CancellationToken.None),
            Throws.InstanceOf<ToolException>(),
            "A refusal is a sentence for the model, never a crash and never an execution.");
    }

    [Test]
    public void The_calculator_refuses_a_missing_or_too_long_expression_and_deep_nesting()
    {
        var tool = new CalculatorTool();
        Assert.Multiple(() =>
        {
            Assert.That(async () => await tool.InvokeAsync(NoArguments, CancellationToken.None), Throws.InstanceOf<ToolException>());
            Assert.That(async () => await tool.InvokeAsync(Expression(new string('1', 501)), CancellationToken.None), Throws.InstanceOf<ToolException>());
            Assert.That(async () => await tool.InvokeAsync(Expression(new string('(', 200) + "1" + new string(')', 200)), CancellationToken.None), Throws.InstanceOf<ToolException>());
            Assert.That(async () => await tool.InvokeAsync(Expression(new string('-', 400) + "1"), CancellationToken.None), Throws.InstanceOf<ToolException>());
        });
    }

    [Test]
    public async Task The_calculator_writes_numbers_the_same_way_in_every_culture()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-CA");
            Assert.That(await new CalculatorTool().InvokeAsync(Expression("1.5 + 1"), CancellationToken.None), Is.EqualTo("2.5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    /// <summary>A clock stopped at one moment, in one time zone.</summary>
    private sealed class FixedTime(DateTimeOffset now, TimeZoneInfo zone) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => zone;

        public override DateTimeOffset GetUtcNow() => now;
    }
}
