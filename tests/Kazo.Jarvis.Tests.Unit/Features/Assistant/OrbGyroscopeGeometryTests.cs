using System.Globalization;
using Kazo.Jarvis.Features.Assistant.Orb;

namespace Kazo.Jarvis.Tests.Unit.Features.Assistant;

/// <summary>The gyroscope rings of the high-quality orb, computed in C# (UI catalog, section 2).</summary>
[TestFixture]
public sealed class OrbGyroscopeGeometryTests
{
    [Test]
    public void The_five_rings_turn_at_the_catalog_speeds()
    {
        var rings = OrbGyroscopeGeometry.Rings;

        Assert.Multiple(() =>
        {
            Assert.That(rings.Select(ring => ring.Kind), Is.EqualTo(new[] { "scale", "arcs", "dash", "band", "inner" }));
            Assert.That(rings.Select(ring => ring.Spin), Is.EqualTo(new[] { 0.12, -0.35, 0.5, -0.7, 0.9 }));
            Assert.That(rings.Select(ring => ring.Direction), Is.EqualTo(new[] { 1, -1, 1, -1, 1 }));
            Assert.That(rings.Count(ring => ring.Flat), Is.EqualTo(1), "Only the inner crown faces the screen.");

            // 2π / 0.12 rad/s = 52.4 s per turn.
            Assert.That(rings[0].TurnMilliseconds, Is.EqualTo("52360"));
        });
    }

    [Test]
    public void Angles_start_on_the_right_and_turn_clockwise()
    {
        var right = OrbGyroscopeGeometry.Point(100, 0);
        var bottom = OrbGyroscopeGeometry.Point(100, 90);

        Assert.Multiple(() =>
        {
            Assert.That(right.X, Is.EqualTo(600).Within(1e-9));
            Assert.That(right.Y, Is.EqualTo(500).Within(1e-9));
            Assert.That(bottom.X, Is.EqualTo(500).Within(1e-9));
            Assert.That(bottom.Y, Is.EqualTo(600).Within(1e-9));
        });
    }

    [Test]
    public void The_drawings_have_the_catalog_counts()
    {
        static int Moves(string path) => path.Count(character => character == 'M');

        Assert.Multiple(() =>
        {
            Assert.That(Moves(OrbGyroscopeGeometry.ScaleTicks.Major), Is.EqualTo(12));
            Assert.That(Moves(OrbGyroscopeGeometry.ScaleTicks.Fine), Is.EqualTo(168));
            Assert.That(OrbGyroscopeGeometry.ScaleLabels.Select(label => label.Text), Is.EqualTo(Enumerable.Range(0, 12).Select(i => $"{i * 30}°")));
            Assert.That(OrbGyroscopeGeometry.Arcs, Has.Count.EqualTo(5));
            Assert.That(OrbGyroscopeGeometry.DashNodes, Has.Count.EqualTo(4));
            Assert.That(Moves(OrbGyroscopeGeometry.BandMarks), Is.EqualTo(24));
            Assert.That(Moves(OrbGyroscopeGeometry.InnerTicks.Major), Is.EqualTo(12));
            Assert.That(Moves(OrbGyroscopeGeometry.InnerTicks.Minor), Is.EqualTo(60));
        });
    }

    [Test]
    public void Numbers_stay_invariant_under_a_French_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-CA");
            Assert.Multiple(() =>
            {
                Assert.That(OrbGyroscopeGeometry.Arc(455, 20, 110), Does.Not.Match(@"\d,\d+,\d").And.Contain("."));
                Assert.That(OrbGyroscopeGeometry.N(12.5), Is.EqualTo("12.5"));
                Assert.That(OrbGyroscopeGeometry.ScaleLabels[1].Transform, Does.StartWith("rotate(120 "));
            });
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
