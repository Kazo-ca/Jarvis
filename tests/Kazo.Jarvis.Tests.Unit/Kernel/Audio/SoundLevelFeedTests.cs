using Kazo.Jarvis.Kernel.Audio;
using Microsoft.Extensions.Time.Testing;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Audio;

/// <summary>The sound level contract: 0 to 1, at most 30 values per second (ADR 0005, section 5).</summary>
[TestFixture]
public sealed class SoundLevelFeedTests
{
    private FakeTimeProvider time = null!;
    private SoundLevelFeed feed = null!;
    private List<SoundLevel> received = null!;
    private IDisposable subscription = null!;

    [SetUp]
    public void SetUp()
    {
        time = new FakeTimeProvider();
        feed = new SoundLevelFeed(time);
        received = [];
        subscription = feed.Subscribe(received.Add);
    }

    [TearDown]
    public void TearDown() => subscription.Dispose();

    [TestCase(-0.5f, 0f)]
    [TestCase(0.42f, 0.42f)]
    [TestCase(3f, 1f)]
    [TestCase(float.NaN, 0f)]
    public void A_level_is_clamped_between_0_and_1(float reported, float expected)
    {
        feed.Report(SoundOrigin.Microphone, reported);

        Assert.That(received, Is.EqualTo(new[] { new SoundLevel(SoundOrigin.Microphone, expected) }));
    }

    [Test]
    public void At_most_30_values_per_second_per_origin()
    {
        // 100 values per second for one second.
        for (var tick = 0; tick < 100; tick++)
        {
            feed.Report(SoundOrigin.Voice, 0.5f);
            time.Advance(TimeSpan.FromMilliseconds(10));
        }

        Assert.That(received, Has.Count.InRange(25, SoundLevelFeed.MaximumRatePerSecond));
    }

    [Test]
    public void Each_origin_has_its_own_rate()
    {
        feed.Report(SoundOrigin.Microphone, 0.3f);
        feed.Report(SoundOrigin.Voice, 0.6f);
        feed.Report(SoundOrigin.Microphone, 0.4f);

        Assert.That(received, Is.EqualTo(new[] { new SoundLevel(SoundOrigin.Microphone, 0.3f), new SoundLevel(SoundOrigin.Voice, 0.6f) }));
    }

    [Test]
    public void Silence_always_passes_so_the_orb_never_stays_lit()
    {
        feed.Report(SoundOrigin.Voice, 0.8f);
        feed.Report(SoundOrigin.Voice, 0f);

        Assert.That(received.Select(level => level.Value), Is.EqualTo(new[] { 0.8f, 0f }));
    }

    [Test]
    public void A_value_passes_again_after_the_interval()
    {
        feed.Report(SoundOrigin.Microphone, 0.1f);
        time.Advance(TimeSpan.FromMilliseconds(34));
        feed.Report(SoundOrigin.Microphone, 0.2f);

        Assert.That(received.Select(level => level.Value), Is.EqualTo(new[] { 0.1f, 0.2f }));
    }

    [Test]
    public void A_disposed_subscription_receives_nothing()
    {
        subscription.Dispose();

        feed.Report(SoundOrigin.Microphone, 0.5f);

        Assert.That(received, Is.Empty);
    }
}
