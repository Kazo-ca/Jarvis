using Kazo.Jarvis.Features.Maintenance.Updates;
using static Kazo.Jarvis.Tests.Unit.Features.Maintenance.Updates.FakeReleaseSource;

namespace Kazo.Jarvis.Tests.Unit.Features.Maintenance.Updates;

[TestFixture]
public sealed class UpdateSelectorTests
{
    private static readonly ReleaseVersion Current = new(new Version(0, 2, 0), null);

    [Test]
    public void Picks_the_newest_newer_release_whatever_the_order()
    {
        var update = UpdateSelector.SelectNewest([Release("v0.3.0"), Release("v0.5.0"), Release("v0.4.0")], Current, includePreReleases: false);

        Assert.That(update?.Version.ToString(), Is.EqualTo("0.5.0"));
        Assert.That(update?.Package.Name, Is.EqualTo("Kazo.Jarvis-0.5.0-win-x64.zip"));
    }

    [Test]
    public void Nothing_when_the_running_version_is_the_newest() =>
        Assert.That(UpdateSelector.SelectNewest([Release("v0.2.0"), Release("v0.1.0")], Current, includePreReleases: true), Is.Null);

    [Test]
    public void Drafts_are_never_offered() =>
        Assert.That(UpdateSelector.SelectNewest([Release("v0.9.0", draft: true)], Current, includePreReleases: true), Is.Null);

    [Test]
    public void Pre_releases_only_when_asked()
    {
        PublishedReleaseList releases = [Release("v0.3.0-beta.1", preRelease: true)];

        Assert.Multiple(() =>
        {
            Assert.That(UpdateSelector.SelectNewest(releases, Current, includePreReleases: false), Is.Null);
            Assert.That(UpdateSelector.SelectNewest(releases, Current, includePreReleases: true)?.Version.ToString(), Is.EqualTo("0.3.0-beta.1"));
        });
    }

    [Test]
    public void A_release_without_a_package_for_this_pc_is_skipped()
    {
        var update = UpdateSelector.SelectNewest(
            [Release("v0.4.0", packageName: "Kazo.Jarvis-0.4.0-win-arm64.zip"), Release("v0.3.0"), Release("notes", packageName: "readme.txt")],
            Current,
            includePreReleases: true);

        Assert.That(update?.Version.ToString(), Is.EqualTo("0.3.0"));
    }

    [Test]
    public void An_unknown_running_version_accepts_any_release() =>
        Assert.That(UpdateSelector.SelectNewest([Release("v0.0.1")], current: null, includePreReleases: true), Is.Not.Null);

    private sealed class PublishedReleaseList : List<Kazo.Jarvis.Kernel.Updates.PublishedRelease>;
}
