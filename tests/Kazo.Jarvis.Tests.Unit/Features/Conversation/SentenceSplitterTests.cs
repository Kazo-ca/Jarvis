using Kazo.Jarvis.Features.Conversation.Dialogue;

namespace Kazo.Jarvis.Tests.Unit.Features.Conversation;

/// <summary>The streamed answer cut into sentences to say (US1.9).</summary>
[TestFixture]
public sealed class SentenceSplitterTests
{
    [Test]
    public void A_sentence_is_given_as_soon_as_the_next_one_starts()
    {
        var splitter = new SentenceSplitter();

        Assert.Multiple(() =>
        {
            Assert.That(splitter.Push("Bien sûr"), Is.Empty);
            Assert.That(splitter.Push(", Monsieur."), Is.Empty, "the next piece may continue it");
            Assert.That(splitter.Push(" Il fait"), Is.EqualTo(new[] { "Bien sûr, Monsieur." }));
            Assert.That(splitter.Push(" beau ! Et vous ? Très"), Is.EqualTo(new[] { "Il fait beau !", "Et vous ?" }));
            Assert.That(splitter.Flush(), Is.EqualTo(new[] { "Très" }));
            Assert.That(splitter.Flush(), Is.Empty);
        });
    }

    [TestCase("Bonjour M. Dupont, ravi de vous voir. Voilà.", "Bonjour M. Dupont, ravi de vous voir.")]
    [TestCase("Il fait 3.5 degrés dehors. Couvrez-vous.", "Il fait 3.5 degrés dehors.")]
    [TestCase("Le livre de J. R. R. Tolkien est long. Oui.", "Le livre de J. R. R. Tolkien est long.")]
    [TestCase("Ask Dr. Watson about it. Then come back.", "Ask Dr. Watson about it.")]
    [TestCase("« C'est parfait. » Il sourit.", "« C'est parfait. »")]
    [TestCase("Attendez… Je vérifie.", "Attendez…")]
    public void Abbreviations_numbers_and_initials_do_not_end_a_sentence(string answer, string first)
    {
        var splitter = new SentenceSplitter();

        var sentences = splitter.Push(answer).Concat(splitter.Flush()).ToArray();

        Assert.That(sentences[0], Is.EqualTo(first));
    }

    [Test]
    public void A_line_break_ends_a_sentence_and_list_marks_are_not_said()
    {
        var splitter = new SentenceSplitter();

        var sentences = splitter.Push("Voici :\n1. Le **pain**\n- le `lait`\n## Fin").Concat(splitter.Flush()).ToArray();

        Assert.That(sentences, Is.EqualTo(new[] { "Voici :", "Le pain", "le lait", "Fin" }));
    }

    [Test]
    public void A_very_long_sentence_is_said_up_to_a_comma()
    {
        var splitter = new SentenceSplitter();
        var words = string.Join(' ', Enumerable.Repeat("mot", 60)) + ", " + string.Join(' ', Enumerable.Repeat("autre", 40));

        var sentences = splitter.Push(words);

        Assert.Multiple(() =>
        {
            Assert.That(sentences, Has.Count.EqualTo(1));
            Assert.That(sentences[0], Does.EndWith("mot,"));
            Assert.That(sentences[0].Length, Is.LessThanOrEqualTo(SentenceSplitter.LongestSentence));
        });
    }

    [TestCase("**", "")]
    [TestCase(" ... ", "")]
    [TestCase("  Très   bien.  ", "Très bien.")]
    [TestCase("*Il sourit* puis répond.", "Il sourit puis répond.")]
    public void Only_what_can_be_said_is_kept(string text, string said) =>
        Assert.That(SentenceSplitter.Speakable(text), Is.EqualTo(said));
}
