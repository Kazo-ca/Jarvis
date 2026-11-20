using System.Globalization;
using Kazo.Jarvis.Kernel.Localization;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Localization;

/// <summary>The language of the assistant (ADR 0008, section 1).</summary>
[TestFixture]
public sealed class AssistantLanguageTests
{
    [TestCase("fr-FR", "fr")]
    [TestCase("fr-CA", "fr")]
    [TestCase("fr", "fr")]
    [TestCase("en-US", "en")]
    [TestCase("de-DE", "en")]
    [TestCase("", "en")]
    public void Windows_in_French_proposes_French_and_anything_else_English(string windows, string expected) =>
        Assert.That(AssistantLanguage.ProposedFor(CultureInfo.GetCultureInfo(windows)).Code, Is.EqualTo(expected));

    [TestCase("fr", true)]
    [TestCase("FR", true)]
    [TestCase("en", true)]
    [TestCase("de", false)]
    [TestCase("fr-CA", false)]
    [TestCase(null, false)]
    public void Only_the_supported_codes_parse(string? code, bool expected) =>
        Assert.That(AssistantLanguage.TryParse(code, out _), Is.EqualTo(expected));

    [Test]
    public void The_first_version_has_English_then_French()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AssistantLanguage.Supported.Select(language => language.Code), Is.EqualTo(new[] { "en", "fr" }));
            Assert.That(AssistantLanguage.French.Culture.Name, Is.EqualTo("fr"));
            Assert.That(AssistantLanguage.TryParse("FR", out var french) && ReferenceEquals(french, AssistantLanguage.French), Is.True);
        });
    }
}
