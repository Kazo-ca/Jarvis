using Kazo.Jarvis.Adapters.Settings;
using Kazo.Jarvis.Features.Audio.Devices;
using Kazo.Jarvis.Tests.Unit.Adapters;

namespace Kazo.Jarvis.Tests.Unit.Features.Audio;

/// <summary>The small pieces of the audio slice: level, test chime, output binder.</summary>
[TestFixture]
public sealed class AudioPiecesTests
{
    [Test]
    public void Silence_and_an_empty_block_have_level_zero()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MicrophoneLevel.Of([]), Is.Zero);
            Assert.That(MicrophoneLevel.Of(new float[320]), Is.Zero);
        });
    }

    [TestCase(1f, 1f)]
    [TestCase(0.1f, 2f / 3f)]
    [TestCase(0.001f, 0f)]
    public void The_level_follows_the_decibels(float amplitude, float expected)
    {
        var block = Enumerable.Repeat(amplitude, 320).ToArray();

        Assert.That(MicrophoneLevel.Of(block), Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void The_chime_is_short_soft_and_starts_and_ends_in_silence()
    {
        var chime = OutputChime.Create();
        var samples = chime.Samples.ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(chime.Duration.TotalSeconds, Is.InRange(0.3, 0.5));
            Assert.That(samples.Max(Math.Abs), Is.LessThanOrEqualTo(0.35f));
            Assert.That(Math.Abs(samples[0]), Is.LessThan(0.001f));
            Assert.That(Math.Abs(samples[^1]), Is.LessThan(0.001f));
        });
    }

    [Test]
    public async Task The_output_binder_gives_the_chosen_output_to_the_playback()
    {
        using var folder = new TemporaryFolder();
        var settings = new JsonFileSettingsStore(folder.Paths, [], TimeProvider.System);
        var playback = new FakeAudioPlayback();
        using var binder = new OutputBinder(playback, settings);

        await binder.StartAsync(CancellationToken.None);
        await settings.WriteAsync(AudioDevicesOptions.Section, new AudioDevicesOptions { OutputId = "headphones" });
        await settings.WriteAsync(AudioDevicesOptions.Section, new AudioDevicesOptions { OutputId = "headphones", MicrophoneMuted = true });
        await binder.StopAsync(CancellationToken.None);
        await settings.WriteAsync(AudioDevicesOptions.Section, new AudioDevicesOptions());

        Assert.Multiple(() =>
        {
            // Once the Windows default at start (already selected, so not set again), once the headphones;
            // the mute change does not touch the output; nothing after the stop.
            Assert.That(playback.Selections, Is.EqualTo(new[] { "headphones" }));
            Assert.That(playback.Stops, Is.EqualTo(1));
        });
    }
}
