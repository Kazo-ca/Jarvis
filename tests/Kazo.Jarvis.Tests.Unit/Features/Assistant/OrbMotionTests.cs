using Kazo.Jarvis.Features.Assistant.Orb;
using Kazo.Jarvis.Kernel.Assistant;

namespace Kazo.Jarvis.Tests.Unit.Features.Assistant;

/// <summary>The motion of each look of the light orb (ADR 0011, sections 3 and 5; UI catalog, section 4).</summary>
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
}
