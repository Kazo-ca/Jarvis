using System.Globalization;

namespace Kazo.Jarvis.Features.Assistant.Orb;

/// <summary>
/// The SVG paths of the light orb (UI catalog, section 3), computed once in C# in a 200 × 200 view box
/// centred on (100, 100). Angles are in degrees, clockwise, 0 at the top, like the prototype.
/// </summary>
public static class OrbGeometry
{
    /// <summary>Centre of the view box.</summary>
    public const double Centre = 100;

    /// <summary>Radius of the progress ring, outside the three rings.</summary>
    public const double ProgressRadius = 92;

    /// <summary>Length of the progress ring, for <c>stroke-dasharray</c>.</summary>
    public static double ProgressLength { get; } = 2 * Math.PI * ProgressRadius;

    /// <summary>A point on a circle.</summary>
    /// <param name="radius">Radius in view box units.</param>
    /// <param name="angle">Angle in degrees, clockwise from the top.</param>
    /// <returns>The coordinates.</returns>
    public static (double X, double Y) Point(double radius, double angle)
    {
        var radians = (angle - 90) * Math.PI / 180;
        return (Centre + (radius * Math.Cos(radians)), Centre + (radius * Math.Sin(radians)));
    }

    /// <summary>An arc path, clockwise from <paramref name="from"/> to <paramref name="to"/>.</summary>
    /// <param name="radius">Radius in view box units.</param>
    /// <param name="from">Start angle in degrees.</param>
    /// <param name="to">End angle in degrees, greater than <paramref name="from"/>.</param>
    /// <returns>The <c>d</c> attribute of a <c>path</c>.</returns>
    public static string Arc(double radius, double from, double to)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(to, from);
        var (x1, y1) = Point(radius, from);
        var (x2, y2) = Point(radius, to);
        var large = to - from > 180 ? 1 : 0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"M{x1:0.##},{y1:0.##} A{radius},{radius} 0 {large} 1 {x2:0.##},{y2:0.##}");
    }

    /// <summary>Radial tick marks from <paramref name="radius"/> inwards.</summary>
    /// <param name="radius">Outer end of each tick.</param>
    /// <param name="from">First angle.</param>
    /// <param name="to">Last angle, included.</param>
    /// <param name="step">Angle between two ticks.</param>
    /// <param name="length">Length of a tick.</param>
    /// <returns>The <c>d</c> attribute of one path holding every tick.</returns>
    public static string Ticks(double radius, double from, double to, double step, double length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        var parts = new List<string>();
        for (var angle = from; angle <= to; angle += step)
        {
            parts.Add(Segment(radius, radius - length, angle));
        }

        return string.Join(' ', parts);
    }

    /// <summary>A "tooth" across a ring, at one end of an arc (combination lock look).</summary>
    /// <param name="radius">Radius of the ring.</param>
    /// <param name="angle">Angle of the tooth.</param>
    /// <param name="length">Length across the ring.</param>
    /// <returns>The <c>d</c> attribute of a path.</returns>
    public static string Tooth(double radius, double angle, double length) =>
        Segment(radius + (length / 2), radius - (length / 2), angle);

    /// <summary>Several teeth in one path.</summary>
    /// <param name="radius">Radius of the ring.</param>
    /// <param name="length">Length across the ring.</param>
    /// <param name="angles">Angles of the teeth.</param>
    /// <returns>The <c>d</c> attribute of a path.</returns>
    public static string Teeth(double radius, double length, params double[] angles) =>
        string.Join(' ', angles.Select(angle => Tooth(radius, angle, length)));

    /// <summary>The visible length of the progress ring.</summary>
    /// <param name="progress">From 0 to 1; clamped.</param>
    /// <returns>The <c>stroke-dasharray</c> attribute.</returns>
    public static string ProgressDash(double progress)
    {
        var shown = Math.Clamp(double.IsNaN(progress) ? 0 : progress, 0, 1) * ProgressLength;
        return string.Create(CultureInfo.InvariantCulture, $"{shown:0.##} {ProgressLength:0.##}");
    }

    private static string Segment(double outer, double inner, double angle)
    {
        var (x1, y1) = Point(outer, angle);
        var (x2, y2) = Point(inner, angle);
        return string.Create(CultureInfo.InvariantCulture, $"M{x1:0.##},{y1:0.##} L{x2:0.##},{y2:0.##}");
    }
}
