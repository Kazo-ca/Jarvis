using Kazo.Jarvis.Features.Maintenance.Updates;

namespace Kazo.Jarvis.Tests.Unit.Features.Maintenance.Updates;

[TestFixture]
public sealed class ReleaseVersionTests
{
    [TestCase("v0.2.0", "0.2.0")]
    [TestCase("0.2", "0.2.0")]
    [TestCase("1", "1.0.0")]
    [TestCase("0.2.0-beta.1", "0.2.0-beta.1")]
    [TestCase("0.2.0+abc123", "0.2.0")]
    [TestCase("0.0.1-test.2+3f1e", "0.0.1-test.2")]
    public void Reads_tags_and_assembly_versions(string text, string expected)
    {
        Assert.That(ReleaseVersion.TryParse(text, out var version), Is.True);
        Assert.That(version.ToString(), Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("latest")]
    [TestCase("0.2.0-")]
    public void Refuses_what_is_not_a_version(string? text) =>
        Assert.That(ReleaseVersion.TryParse(text, out _), Is.False);

    [TestCase("0.2.0", "0.1.9")]
    [TestCase("0.10.0", "0.9.0")]
    [TestCase("0.2.0", "0.2.0-rc.1")]
    [TestCase("0.2.0-beta.2", "0.2.0-beta.1")]
    [TestCase("0.2.0-beta.10", "0.2.0-beta.9")]
    [TestCase("0.2.0-beta", "0.2.0-alpha")]
    [TestCase("0.2.0-beta.1", "0.2.0-beta")]
    public void Orders_versions_as_semver(string newer, string older)
    {
        ReleaseVersion.TryParse(newer, out var a);
        ReleaseVersion.TryParse(older, out var b);

        Assert.Multiple(() =>
        {
            Assert.That(a > b, Is.True);
            Assert.That(b < a, Is.True);
        });
    }

    [Test]
    public void Build_metadata_does_not_count()
    {
        ReleaseVersion.TryParse("0.2.0+one", out var a);
        ReleaseVersion.TryParse("v0.2.0", out var b);

        Assert.That(a.CompareTo(b), Is.Zero);
    }
}
