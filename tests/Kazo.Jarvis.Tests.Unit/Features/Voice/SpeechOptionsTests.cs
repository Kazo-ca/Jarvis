using Kazo.Jarvis.Features.Voice.Speech;
using Kazo.Jarvis.Kernel.Localization;
using Kazo.Jarvis.Kernel.Speech;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

[TestFixture]
public sealed class SpeechOptionsTests
{
    [Test]
    public void Each_language_has_its_Piper_voice_by_default()
    {
        var options = SpeechOptions.Section.CreateDefault();

        Assert.Multiple(() =>
        {
            Assert.That(options.ChoiceFor(AssistantLanguage.French), Is.EqualTo(new VoiceChoice("piper", "fr_FR-tom-medium")));
            Assert.That(options.ChoiceFor(AssistantLanguage.English), Is.EqualTo(new VoiceChoice("piper", "en_GB-alan-medium")));
        });
    }

    [Test]
    public void A_choice_changes_one_language_only()
    {
        var options = new SpeechOptions().With(AssistantLanguage.French, new VoiceChoice("kokoro", "ff_siwis", 1.2));

        Assert.Multiple(() =>
        {
            Assert.That(options.ChoiceFor(AssistantLanguage.French), Is.EqualTo(new VoiceChoice("kokoro", "ff_siwis", 1.2)));
            Assert.That(options.ChoiceFor(AssistantLanguage.English), Is.EqualTo(SpeechDefaults.For(AssistantLanguage.English)));
            Assert.That(SpeechOptions.Section.Validate(options), Is.Empty);
        });
    }

    [TestCase("de", "piper", "x", 1.0, "Unknown language 'de'.")]
    [TestCase("fr", "", "x", 1.0, "The voice of 'fr' names no engine or no voice.")]
    [TestCase("fr", "piper", "x", 3.0, "The speed of 'fr' is outside 0.5 to 2.")]
    [TestCase("fr", "piper", "x", double.NaN, "The speed of 'fr' is outside 0.5 to 2.")]
    public void A_wrong_choice_is_refused(string language, string engine, string voice, double speed, string problem)
    {
        var options = new SpeechOptions { Voices = new Dictionary<string, VoiceChoice> { [language] = new(engine, voice, speed) } };

        Assert.That(SpeechOptions.Section.Validate(options), Is.EqualTo(new[] { problem }));
    }

    [Test]
    public void The_speed_is_brought_inside_its_range()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new VoiceChoice("a", "b", 0.1).ClampedSpeed, Is.EqualTo(0.5));
            Assert.That(new VoiceChoice("a", "b", 9).ClampedSpeed, Is.EqualTo(2.0));
            Assert.That(new VoiceChoice("a", "b", double.PositiveInfinity).ClampedSpeed, Is.EqualTo(1.0));
        });
    }
}
