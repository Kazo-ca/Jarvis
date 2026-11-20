using System.Xml.Linq;

namespace Kazo.Jarvis.Tests.Unit.Architecture;

/// <summary>
/// Every text exists in both languages (ADR 0008, section 6): each <c>.resx</c> under <c>src/</c> has its
/// <c>.fr.resx</c> with the same keys, and the reverse. Part of the merge bar.
/// </summary>
[TestFixture]
public sealed class ResourceKeysTests
{
    private static IEnumerable<string> NeutralFiles() =>
        RepositoryLayout.SourceFiles(RepositoryLayout.Src, "*.resx")
            .Where(path => !Path.GetFileName(path).Contains(".fr.", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal);

    [Test]
    public void The_sources_hold_resources()
    {
        // Guards the test below against a wrong folder: work item 5 brought the first resources.
        Assert.That(NeutralFiles(), Is.Not.Empty);
    }

    [TestCaseSource(nameof(NeutralFiles))]
    public void The_French_resources_have_the_same_keys_as_the_English_ones(string neutral)
    {
        var french = Path.ChangeExtension(neutral, ".fr.resx");
        Assert.That(File.Exists(french), Is.True, $"{RepositoryLayout.Relative(french)} is missing.");

        var englishKeys = Keys(neutral);
        var frenchKeys = Keys(french);

        Assert.Multiple(() =>
        {
            Assert.That(englishKeys.Except(frenchKeys), Is.Empty, "Keys missing in French.");
            Assert.That(frenchKeys.Except(englishKeys), Is.Empty, "Keys missing in English.");
            Assert.That(Values(neutral).Concat(Values(french)).Where(value => string.IsNullOrWhiteSpace(value.Value)).Select(value => value.Key), Is.Empty, "Empty texts.");
        });
    }

    [Test]
    public void Every_French_resource_has_its_English_one()
    {
        var orphans = RepositoryLayout.SourceFiles(RepositoryLayout.Src, "*.fr.resx")
            .Where(path => !File.Exists(path[..^".fr.resx".Length] + ".resx"))
            .Select(RepositoryLayout.Relative);

        Assert.That(orphans, Is.Empty);
    }

    private static string[] Keys(string path) => Values(path).Select(value => value.Key).Order(StringComparer.Ordinal).ToArray();

    private static IEnumerable<KeyValuePair<string, string>> Values(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Where(data => data.Attribute("type") is null)
            .Select(data => new KeyValuePair<string, string>((string)data.Attribute("name")!, (string?)data.Element("value") ?? string.Empty));
}
