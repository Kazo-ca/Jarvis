using Kazo.Jarvis.Features.Voice.Speech;
using Kazo.Jarvis.Kernel.Audio;

namespace Kazo.Jarvis.Tests.Unit.Features.Voice;

[TestFixture]
public sealed class VoiceEnvelopeTests
{
    [Test]
    public void One_level_per_thirtieth_of_a_second_loud_then_silent()
    {
        // 0.5 s of a full-scale tone, then 0.5 s of silence, at 24 kHz.
        var samples = new float[24_000];
        for (var i = 0; i < 12_000; i++)
        {
            samples[i] = (float)Math.Sin(i * 0.2);
        }

        var levels = VoiceEnvelope.Compute(new AudioClip(samples, 24_000));

        Assert.Multiple(() =>
        {
            Assert.That(levels, Has.Length.EqualTo(30));
            Assert.That(levels.Take(15), Has.All.GreaterThan(0.9f));
            Assert.That(levels.Skip(15), Has.All.EqualTo(0f));
        });
    }

    [Test]
    public void An_empty_clip_has_no_level()
    {
        Assert.That(VoiceEnvelope.Compute(new AudioClip(Array.Empty<float>(), 22_050)), Is.Empty);
    }
}
