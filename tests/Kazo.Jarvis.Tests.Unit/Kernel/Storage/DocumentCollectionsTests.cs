using Kazo.Jarvis.Kernel.Storage;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Storage;

/// <summary>Collection names and document identifiers (ADR 0004, section 3).</summary>
[TestFixture]
public sealed class DocumentCollectionsTests
{
    [TestCase("Conversation.Turns", true)]
    [TestCase("Memory.Facts2", true)]
    [TestCase("Turns", false)]
    [TestCase("conversation.turns", false)]
    [TestCase("Conversation.Turns; DROP TABLE documents", false)]
    [TestCase("", false)]
    public void Collection_names_are_area_dot_name(string name, bool valid) =>
        Assert.That(DocumentCollections.IsValidName(name), Is.EqualTo(valid));

    [Test]
    public void Identifiers_of_later_documents_sort_after()
    {
        var first = DocumentId.New(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero));
        var second = DocumentId.New(new DateTimeOffset(2026, 11, 19, 10, 0, 1, TimeSpan.Zero));

        Assert.That(string.CompareOrdinal(first.ToString(), second.ToString()), Is.Negative);
    }
}
