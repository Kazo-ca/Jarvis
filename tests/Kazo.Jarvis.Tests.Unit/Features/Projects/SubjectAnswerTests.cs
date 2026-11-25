using Kazo.Jarvis.Features.Projects.View;

namespace Kazo.Jarvis.Tests.Unit.Features.Projects;

/// <summary>The prefix that answers a task of a subject, <c>T3&gt;</c> (ADR 0026, section 4).</summary>
[TestFixture]
public sealed class SubjectAnswerTests
{
    [TestCase("T3> oui, on fait comme ça", 3, "oui, on fait comme ça")]
    [TestCase("t12>d'accord", 12, "d'accord")]
    [TestCase("  T 4 >  vas-y  ", 4, "vas-y")]
    [TestCase("T1> première ligne\nseconde ligne", 1, "première ligne\nseconde ligne")]
    public void A_message_that_begins_with_the_number_of_a_task_answers_that_task(string message, int number, string text)
    {
        Assert.That(SubjectAnswer.Parse(message), Is.EqualTo(new SubjectAnswer(number, text)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("oui, on fait comme ça")]
    [TestCase("T3>")]
    [TestCase("T3>   ")]
    [TestCase("T0> rien")]
    [TestCase("Pour T3> non")]
    [TestCase("T> non")]
    public void Any_other_message_is_for_the_subject(string? message)
    {
        Assert.That(SubjectAnswer.Parse(message), Is.Null);
    }
}
