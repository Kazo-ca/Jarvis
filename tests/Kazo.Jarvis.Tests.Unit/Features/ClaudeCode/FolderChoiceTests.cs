using Kazo.Jarvis.Features.ClaudeCode.Launch;

namespace Kazo.Jarvis.Tests.Unit.Features.ClaudeCode;

/// <summary>The folder a request names (ADR 0021, section 1): compared by code, whatever the spelling of the folder.</summary>
[TestFixture]
public sealed class FolderChoiceTests
{
    private static readonly string[] Folders = ["/home/b/src/kazo-bar", "/home/b/src/Jarvis", "/home/b/Notes", "/home/b/src/KazoOs"];

    [TestCase("corrige le menu de Kazo Bar", "/home/b/src/kazo-bar")]
    [TestCase("Corrige le menu de kazo-bar.", "/home/b/src/kazo-bar")]
    [TestCase("ranger le dépôt Jarvis", "/home/b/src/Jarvis")]
    [TestCase("JARVIS", "/home/b/src/Jarvis")]
    [TestCase("relis mes notés", "/home/b/Notes")]
    [TestCase("dans kazo os, ajoute un test", "/home/b/src/KazoOs")]
    public void A_folder_is_named_by_its_name_whatever_the_case_the_accents_and_the_separators(string request, string expected)
    {
        Assert.That(FolderChoice.Named(request, Folders), Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void A_request_may_name_no_folder_or_several()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FolderChoice.Named("corrige le menu", Folders), Is.Empty);
            Assert.That(FolderChoice.Named("copie de Jarvis vers Notes", Folders), Has.Count.EqualTo(2));
            Assert.That(FolderChoice.Named("un bar à jus", Folders), Is.Empty, "A part of a name is not the name.");
        });
    }

    [TestCase("/home/b/src/kazo-bar", "kazo-bar")]
    [TestCase(@"C:\src\Jarvis\", "Jarvis")]
    [TestCase("/", "/")]
    public void The_name_said_for_a_folder_is_its_last_part(string folder, string name)
    {
        Assert.That(FolderChoice.NameOf(folder), Is.EqualTo(name));
    }
}
