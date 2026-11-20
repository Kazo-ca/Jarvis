using Kazo.Jarvis.Features.Conversation.Dialogue;
using Kazo.Jarvis.Kernel.Localization;

namespace Kazo.Jarvis.Tests.Unit.Features.Conversation;

/// <summary>The personality of each language (US1.15, ADR 0008, section 4).</summary>
[TestFixture]
public sealed class ConversationOptionsTests
{
    [Test]
    public void The_default_personality_is_the_butler_in_each_language()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SystemPrompt.DefaultPersonality(AssistantLanguage.French), Does.Contain("majordome").And.Contain("Monsieur"));
            Assert.That(SystemPrompt.DefaultPersonality(AssistantLanguage.English), Does.Contain("butler").And.Contain("Sir"));
            Assert.That(new ConversationOptions().CustomPersonality(AssistantLanguage.French), Is.Null);
        });
    }

    [Test]
    public void A_personality_is_changed_and_reset_per_language()
    {
        var changed = new ConversationOptions()
            .WithPersonality(AssistantLanguage.French, "  Tu es bref.  ")
            .WithPersonality(AssistantLanguage.English, "You are brief.");
        var reset = changed.WithPersonality(AssistantLanguage.French, null);

        Assert.Multiple(() =>
        {
            Assert.That(changed.CustomPersonality(AssistantLanguage.French), Is.EqualTo("Tu es bref."));
            Assert.That(reset.CustomPersonality(AssistantLanguage.French), Is.Null);
            Assert.That(reset.CustomPersonality(AssistantLanguage.English), Is.EqualTo("You are brief."));
            Assert.That(changed.WithPersonality(AssistantLanguage.English, "   ").CustomPersonality(AssistantLanguage.English), Is.Null);
        });
    }

    [Test]
    public void An_unknown_language_or_a_too_long_text_is_refused()
    {
        var unknown = new ConversationOptions { Personalities = new Dictionary<string, string> { ["de"] = "Du bist Jarvis." } };
        var tooLong = new ConversationOptions().WithPersonality(AssistantLanguage.French, new string('a', ConversationOptions.MaximumPersonalityLength + 1));

        Assert.Multiple(() =>
        {
            Assert.That(ConversationOptions.Section.Validate(unknown), Has.Count.EqualTo(1));
            Assert.That(ConversationOptions.Section.Validate(tooLong), Has.Count.EqualTo(1));
            Assert.That(ConversationOptions.Section.Validate(new ConversationOptions()), Is.Empty);
        });
    }
}
