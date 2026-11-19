using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Settings;

/// <summary>A section is named &lt;Area&gt;.&lt;Slice&gt; and carries its defaults and validation (ADR 0004, section 2).</summary>
[TestFixture]
public sealed class SettingsSectionTests
{
    [TestCase("Settings.Language")]
    [TestCase("Voice.WakeWord")]
    [TestCase("Llm.Providers2")]
    public void A_valid_name_is_accepted(string name) =>
        Assert.That(new SettingsSection<object>(name, () => new object()).Name, Is.EqualTo(name));

    [TestCase("")]
    [TestCase("Language")]
    [TestCase("settings.language")]
    [TestCase("Settings.Language.Extra")]
    [TestCase("Settings.")]
    [TestCase("Settings Language")]
    [TestCase("schemaVersion")]
    public void An_invalid_name_is_refused(string name) =>
        Assert.That(() => new SettingsSection<object>(name, () => new object()), Throws.ArgumentException);

    [Test]
    public void Defaults_are_a_new_instance_each_time()
    {
        var section = new SettingsSection<List<int>>("Test.Values", () => [1]);

        var first = section.CreateDefault();
        var second = section.CreateDefault();

        Assert.That(first, Is.Not.SameAs(second));
    }

    [Test]
    public void Validation_returns_the_problems_and_nothing_when_omitted()
    {
        var checkedSection = new SettingsSection<string>("Test.Text", () => "a", value => value.Length > 3 ? ["Too long."] : []);
        var unvalidated = new SettingsSection<string>("Test.Other", () => "a");

        Assert.Multiple(() =>
        {
            Assert.That(checkedSection.Validate("abcd"), Is.EqualTo(new[] { "Too long." }));
            Assert.That(checkedSection.Validate("abc"), Is.Empty);
            Assert.That(unvalidated.Validate("anything"), Is.Empty);
        });
    }
}
