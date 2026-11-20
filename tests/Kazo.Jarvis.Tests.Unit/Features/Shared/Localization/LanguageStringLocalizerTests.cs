using Kazo.Jarvis.Features.Settings.Language;
using Kazo.Jarvis.Features.Shared.Localization;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Tests.Unit.Features.Settings;
using Kazo.Jarvis.Tests.Unit.Kernel;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Tests.Unit.Features.Shared.Localization;

/// <summary>Texts in the language of the assistant (ADR 0008, section 2), whatever the thread culture.</summary>
[TestFixture]
public sealed class LanguageStringLocalizerTests
{
    private ManualLanguage language = null!;
    private ListLogger log = null!;
    private LanguageStringLocalizerFactory factory = null!;

    [SetUp]
    public void SetUp()
    {
        language = new ManualLanguage();
        log = new ListLogger();
        factory = new LanguageStringLocalizerFactory(language, log);
    }

    [TearDown]
    public void TearDown() => log.Dispose();

    [Test]
    public void The_same_localizer_follows_a_language_change()
    {
        var texts = new LanguageStringLocalizer<LanguageSettingsPage>(factory);

        var english = texts["Title"].Value;
        language.Current = AssistantLanguage.French;
        var french = texts["Title"].Value;

        Assert.Multiple(() =>
        {
            Assert.That(english, Is.EqualTo("Language"));
            Assert.That(french, Is.EqualTo("Langue"));
        });
    }

    [Test]
    public void Arguments_are_placed_in_the_text()
    {
        language.Current = AssistantLanguage.French;
        var texts = factory.Create(typeof(FallbackTexts));

        Assert.That(texts["Greeting", "Monsieur"].Value, Is.EqualTo("Bonsoir, Monsieur."));
    }

    [Test]
    public void A_missing_French_text_shows_the_English_one_and_is_logged()
    {
        language.Current = AssistantLanguage.French;
        var texts = factory.Create(typeof(FallbackTexts));

        var text = texts["OnlyInEnglish"];

        Assert.Multiple(() =>
        {
            Assert.That(text.Value, Is.EqualTo("Only in English"));
            Assert.That(text.ResourceNotFound, Is.False);
            Assert.That(log.Entries.Any(entry => entry.Level == LogLevel.Debug && entry.Message.Contains("OnlyInEnglish", StringComparison.Ordinal)), Is.True);
        });
    }

    [Test]
    public void A_missing_key_shows_its_name_and_is_logged_as_a_warning()
    {
        var texts = factory.Create(typeof(FallbackTexts));

        var text = texts["NoSuchKey"];

        Assert.Multiple(() =>
        {
            Assert.That(text.Value, Is.EqualTo("NoSuchKey"));
            Assert.That(text.ResourceNotFound, Is.True);
            Assert.That(log.Entries.Any(entry => entry.Level == LogLevel.Warning), Is.True);
        });
    }

    [Test]
    public void All_the_texts_of_a_language_are_listed()
    {
        language.Current = AssistantLanguage.French;
        var texts = factory.Create(typeof(FallbackTexts));

        var all = texts.GetAllStrings(includeParentCultures: true).ToDictionary(text => text.Name, text => text.Value);

        Assert.That(all, Is.EqualTo(new Dictionary<string, string>
        {
            ["Greeting"] = "Bonsoir, {0}.",
            ["OnlyInEnglish"] = "Only in English",
        }));
    }
}
