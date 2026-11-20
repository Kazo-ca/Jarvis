using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Assistant.Orb;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Tests.Unit.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Tests.Unit.Features.Assistant;

/// <summary>The "orb quality" setting (ADR 0011, section 1): high quality by default, changed without restart.</summary>
[TestFixture]
public sealed class OrbQualityKeeperTests
{
    private TemporaryFolder folder = null!;
    private ISettingsStore settings = null!;

    [SetUp]
    public void SetUp()
    {
        folder = new TemporaryFolder();
        settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
    }

    [TearDown]
    public void TearDown() => folder.Dispose();

    [Test]
    public void High_quality_is_the_default()
    {
        using var keeper = new OrbQualityKeeper(settings, NullLogger<OrbQualityKeeper>.Instance);

        Assert.That(keeper.Current, Is.EqualTo(OrbQuality.High));
    }

    [Test]
    public async Task A_saved_choice_is_read_at_start()
    {
        await settings.WriteAsync(OrbOptions.Section, new OrbOptions { Quality = OrbQuality.Light });

        using var keeper = new OrbQualityKeeper(settings, NullLogger<OrbQualityKeeper>.Instance);

        Assert.That(keeper.Current, Is.EqualTo(OrbQuality.Light));
    }

    [Test]
    public async Task A_new_choice_applies_at_once_and_is_announced()
    {
        using var keeper = new OrbQualityKeeper(settings, NullLogger<OrbQualityKeeper>.Instance);
        var announced = new List<OrbQuality>();
        keeper.Changed += announced.Add;

        var result = await keeper.ChooseAsync(OrbQuality.Light);
        await keeper.ChooseAsync(OrbQuality.Light);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.True);
            Assert.That(keeper.Current, Is.EqualTo(OrbQuality.Light));
            Assert.That(announced, Is.EqualTo(new[] { OrbQuality.Light }), "Saving the same value again announces nothing.");
            Assert.That(settings.Read(OrbOptions.Section).Quality, Is.EqualTo(OrbQuality.Light));
        });
    }

    [Test]
    public async Task An_unknown_quality_is_refused()
    {
        var result = await settings.WriteAsync(OrbOptions.Section, new OrbOptions { Quality = (OrbQuality)7 });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSaved, Is.False);
            Assert.That(settings.Read(OrbOptions.Section).Quality, Is.EqualTo(OrbQuality.High));
        });
    }
}
