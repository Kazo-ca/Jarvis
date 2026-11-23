using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Kernel.Tools;
using Kazo.Jarvis.Tests.Unit.Adapters;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Tools;

/// <summary>The registry of the tools (ADR 0017, sections 1, 4 and 6): what is known, and what is given to the model.</summary>
[TestFixture]
public sealed class ToolRegistryTests
{
    private TemporaryFolder folder = null!;
    private JsonFileSettingsStore settings = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public void Only_the_tools_enabled_by_default_are_given_to_the_model_until_the_user_chooses()
    {
        var local = new FakeTool("get_datetime");
        var leaving = new FakeTool("web_search") { EnabledByDefault = false };
        using var registry = new ToolRegistry([leaving, local], settings);

        Assert.Multiple(() =>
        {
            Assert.That(registry.Tools.Select(tool => tool.Name), Is.EqualTo(new[] { "get_datetime", "web_search" }), "Every tool is known, by name.");
            Assert.That(registry.Enabled, Is.EqualTo(new[] { local }), "A tool that makes something leave the machine is off by default.");
            Assert.That(registry.Find("web_search"), Is.SameAs(leaving));
            Assert.That(registry.Find("Get_Datetime"), Is.Null, "The name is exact.");
        });
    }

    [Test]
    public async Task A_switch_of_the_settings_wins_over_the_default_and_is_announced()
    {
        var local = new FakeTool("get_datetime");
        var leaving = new FakeTool("web_search") { EnabledByDefault = false };
        using var registry = new ToolRegistry([local, leaving], settings);
        var changes = 0;
        registry.Changed += () => changes++;

        await settings.WriteAsync(ToolActivation.Section, new ToolActivation().With("web_search", true).With("get_datetime", false));

        Assert.Multiple(() =>
        {
            Assert.That(registry.Enabled, Is.EqualTo(new[] { leaving }));
            Assert.That(registry.IsEnabled(local), Is.False);
            Assert.That(changes, Is.EqualTo(1));
        });
    }

    [Test]
    public void A_tool_found_later_is_added_then_removed_and_a_name_is_taken_once()
    {
        using var registry = new ToolRegistry([new FakeTool("get_datetime")], settings);
        var found = new FakeTool("HassTurnOn") { Origin = ToolOrigins.Mcp("maison"), EnabledByDefault = false };

        var registration = registry.Register(found);
        Assert.Multiple(() =>
        {
            Assert.That(registry.Find("HassTurnOn"), Is.SameAs(found));
            Assert.That(found.Origin, Is.EqualTo("mcp:maison"));
            Assert.That(registry.Enabled.Select(tool => tool.Name), Is.EqualTo(new[] { "get_datetime" }), "An MCP tool is off when found (ADR 0018).");
            Assert.That(() => registry.Register(new FakeTool("get_datetime")), Throws.ArgumentException);
        });

        registration.Dispose();
        Assert.That(registry.Find("HassTurnOn"), Is.Null);
    }
}
