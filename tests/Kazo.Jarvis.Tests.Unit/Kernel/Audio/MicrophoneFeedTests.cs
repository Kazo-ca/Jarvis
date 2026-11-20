using Kazo.Jarvis.Kernel.Audio;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Audio;

/// <summary>The microphone stream of the kernel: ring buffer and subscribers (ADR 0006, section 1).</summary>
[TestFixture]
public sealed class MicrophoneFeedTests
{
    // 1 ms of audio: 16 samples, small enough to wrap around in the tests.
    private static readonly TimeSpan OneMillisecond = TimeSpan.FromMilliseconds(1);

    [Test]
    public void The_latest_samples_come_back_oldest_first()
    {
        var feed = new MicrophoneFeed(OneMillisecond);
        feed.Write(Range(1, 10));

        var copy = new float[4];
        Assert.Multiple(() =>
        {
            Assert.That(feed.CopyLatest(copy), Is.EqualTo(4));
            Assert.That(copy, Is.EqualTo(new float[] { 7, 8, 9, 10 }));
        });
    }

    [Test]
    public void The_buffer_wraps_around_and_keeps_only_its_length()
    {
        var feed = new MicrophoneFeed(OneMillisecond);
        feed.Write(Range(1, 10));
        feed.Write(Range(11, 20));

        var copy = new float[20];
        var count = feed.CopyLatest(copy);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(16));
            Assert.That(copy[4..], Is.EqualTo(Range(15, 16)));
            Assert.That(feed.Written, Is.EqualTo(30));
        });
    }

    [Test]
    public void A_block_longer_than_the_buffer_keeps_its_end()
    {
        var feed = new MicrophoneFeed(OneMillisecond);
        feed.Write(Range(1, 40));

        var copy = new float[16];
        feed.CopyLatest(copy);

        Assert.That(copy, Is.EqualTo(Range(25, 16)));
    }

    [Test]
    public void Clear_forgets_what_was_heard_but_keeps_the_position()
    {
        var feed = new MicrophoneFeed(OneMillisecond);
        feed.Write(Range(1, 10));

        feed.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(feed.CopyLatest(new float[4]), Is.Zero);
            Assert.That(feed.Written, Is.EqualTo(10));
        });
    }

    [Test]
    public void Subscribers_receive_each_block_until_they_unsubscribe()
    {
        var feed = new MicrophoneFeed(OneMillisecond);
        var received = new List<float>();
        var subscription = feed.Subscribe(samples => received.AddRange(samples.ToArray()));

        feed.Write(Range(1, 3));
        subscription.Dispose();
        feed.Write(Range(4, 3));

        Assert.That(received, Is.EqualTo(Range(1, 3)));
    }

    [Test]
    public void The_default_buffer_holds_thirty_seconds_at_sixteen_kilohertz()
    {
        var feed = new MicrophoneFeed();
        feed.Write(new float[MicrophoneFormat.SampleRate * 31]);

        Assert.That(feed.CopyLatest(new float[MicrophoneFormat.SampleRate * 40]), Is.EqualTo(MicrophoneFormat.SampleRate * 30));
    }

    private static float[] Range(int start, int count) => [.. Enumerable.Range(start, count).Select(value => (float)value)];
}
