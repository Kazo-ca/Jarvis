using Kazo.Jarvis.Features.Assistant.Orb;

namespace Kazo.Jarvis.Tests.Unit.Features.Assistant;

/// <summary>The SVG paths of the light orb, computed in C# (UI catalog, section 3).</summary>
[TestFixture]
public sealed class OrbGeometryTests
{
    [Test]
    public void Angles_start_at_the_top_and_turn_clockwise()
    {
        var top = OrbGeometry.Point(50, 0);
        var right = OrbGeometry.Point(50, 90);

        Assert.Multiple(() =>
        {
            Assert.That(top.X, Is.EqualTo(100).Within(1e-9));
            Assert.That(top.Y, Is.EqualTo(50).Within(1e-9));
            Assert.That(right.X, Is.EqualTo(150).Within(1e-9));
            Assert.That(right.Y, Is.EqualTo(100).Within(1e-9));
        });
    }

    [Test]
    public void An_arc_uses_the_large_flag_beyond_half_a_turn_and_invariant_numbers()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OrbGeometry.Arc(44, 30, 330), Does.Contain(" 0 1 1 "));
            Assert.That(OrbGeometry.Arc(84, 140, 175), Does.Contain(" 0 0 1 "));
            Assert.That(OrbGeometry.Arc(38.5, 40, 320), Does.StartWith("M").And.Not.Contain(";"));
        });
    }

    [Test]
    public void Arcs_stay_invariant_under_a_French_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-CA");
            Assert.That(OrbGeometry.Arc(38.5, 40, 320), Does.Contain("38.5"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Test]
    public void Ticks_hold_one_segment_per_step_both_ends_included()
    {
        var ticks = OrbGeometry.Ticks(78, 0, 90, 30, 6);

        Assert.That(ticks.Split('M', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(4));
    }

    [TestCase(0.0, "0 ")]
    [TestCase(-1.0, "0 ")]
    [TestCase(double.NaN, "0 ")]
    public void The_progress_dash_is_clamped(double progress, string start)
    {
        Assert.That(OrbGeometry.ProgressDash(progress), Does.StartWith(start));
    }

    [Test]
    public void A_full_progress_shows_the_whole_ring()
    {
        var parts = OrbGeometry.ProgressDash(2).Split(' ');

        Assert.That(parts[0], Is.EqualTo(parts[1]));
    }
}
