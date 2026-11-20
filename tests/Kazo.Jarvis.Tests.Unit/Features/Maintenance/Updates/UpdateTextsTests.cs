using System.Globalization;
using System.Xml.Linq;
using Kazo.Jarvis.Features.Maintenance.Updates;
using Kazo.Jarvis.Tests.Unit.Architecture;

namespace Kazo.Jarvis.Tests.Unit.Features.Maintenance.Updates;

/// <summary>Every text of the updates slice exists in English and in French (ADR 0008).</summary>
[TestFixture]
public sealed class UpdateTextsTests
{
    private static string Folder => Path.Combine(RepositoryLayout.ProjectFolder("Kazo.Jarvis.Features"), "Maintenance", "Updates");

    [Test]
    public void English_and_french_have_the_same_keys()
    {
        Assert.That(Keys("UpdateTexts.fr.resx"), Is.EquivalentTo(Keys("UpdateTexts.resx")));
    }

    [Test]
    public void The_texts_follow_the_ui_language()
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("fr-CA");
            var french = UpdateTexts.InstallButton("0.2.0");
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            var english = UpdateTexts.InstallButton("0.2.0");

            Assert.Multiple(() =>
            {
                Assert.That(french, Is.EqualTo("Installer la version 0.2.0"));
                Assert.That(english, Is.EqualTo("Install version 0.2.0"));
            });
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    private static IEnumerable<string> Keys(string file) =>
        XDocument.Load(Path.Combine(Folder, file)).Root!.Elements("data").Select(data => (string)data.Attribute("name")!);
}
