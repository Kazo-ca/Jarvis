using Kazo.Jarvis.Features.Assistant.Orb;
using Kazo.Jarvis.Kernel.Assistant;

namespace Kazo.Jarvis.Tests.Unit.Features.Assistant;

/// <summary>The motion of each look, in both versions of the orb (ADR 0011, sections 3 and 5; UI catalog, section 4).</summary>
[TestFixture]
public sealed class OrbMotionTests
{
    [TestCase(AssistantLook.Listening, 1.0, false)]
    [TestCase(AssistantLook.Thinking, 3.5, false)]
    [TestCase(AssistantLook.Speaking, 1.2, true)]
    [TestCase(AssistantLook.LongTask, 0.5, false)]
    public void An_animated_look_rotates_at_the_catalog_speed(AssistantLook look, double rate, bool pulses)
    {
        Assert.That(OrbMotion.For(look, animateIdle: false, reducedMotion: false), Is.EqualTo(new OrbMotion(rate, pulses)));
    }

    [TestCase(AssistantLook.Idle)]
    [TestCase(AssistantLook.Error)]
    [TestCase(AssistantLook.MicrophoneMuted)]
    public void Idle_error_and_muted_are_a_still_image(AssistantLook look)
    {
        Assert.That(OrbMotion.For(look, animateIdle: false, reducedMotion: false).IsStill, Is.True);
    }

    [Test]
    public void Only_the_idle_look_follows_the_host_animation_rule()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OrbMotion.For(AssistantLook.Idle, animateIdle: true, reducedMotion: false).Rate, Is.EqualTo(OrbMotion.AnimatedIdleRate));
            Assert.That(OrbMotion.For(AssistantLook.Error, animateIdle: true, reducedMotion: false).IsStill, Is.True);
            Assert.That(OrbMotion.For(AssistantLook.MicrophoneMuted, animateIdle: true, reducedMotion: false).IsStill, Is.True);
        });
    }

    [Test]
    public void Reduced_motion_stops_every_look([Values] AssistantLook look, [Values] bool animateIdle)
    {
        Assert.That(OrbMotion.For(look, animateIdle, reducedMotion: true), Is.EqualTo(OrbMotion.Still));
    }

    [TestCase(AssistantLook.Listening, 0.8)]
    [TestCase(AssistantLook.Thinking, 2.4)]
    [TestCase(AssistantLook.Speaking, 1.0)]
    [TestCase(AssistantLook.LongTask, 0.45)]
    public void High_quality_rotates_at_its_catalog_speed_and_never_pulses(AssistantLook look, double rate)
    {
        Assert.That(OrbMotion.For(look, OrbQuality.High, animateIdle: false, reducedMotion: false), Is.EqualTo(new OrbMotion(rate, false)));
    }

    [Test]
    public void Both_versions_keep_idle_error_and_muted_still([Values] OrbQuality quality)
    {
        Assert.Multiple(() =>
        {
            Assert.That(OrbMotion.For(AssistantLook.Idle, quality, animateIdle: false, reducedMotion: false).IsStill, Is.True);
            Assert.That(OrbMotion.For(AssistantLook.Error, quality, animateIdle: true, reducedMotion: false).IsStill, Is.True);
            Assert.That(OrbMotion.For(AssistantLook.MicrophoneMuted, quality, animateIdle: true, reducedMotion: false).IsStill, Is.True);
        });
    }

    [Test]
    public void The_animated_idle_look_of_the_high_quality_turns_at_its_own_speed()
    {
        Assert.That(
            OrbMotion.For(AssistantLook.Idle, OrbQuality.High, animateIdle: true, reducedMotion: false).Rate,
            Is.EqualTo(OrbMotion.AnimatedIdleRateHighQuality));
    }

    [Test]
    public void Reduced_motion_stops_the_high_quality_too([Values] AssistantLook look)
    {
        Assert.That(OrbMotion.For(look, OrbQuality.High, animateIdle: true, reducedMotion: true), Is.EqualTo(OrbMotion.Still));
    }
}
