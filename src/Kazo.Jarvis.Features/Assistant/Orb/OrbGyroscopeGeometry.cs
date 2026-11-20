using System.Globalization;
using System.Text;

namespace Kazo.Jarvis.Features.Assistant.Orb;

/// <summary>
/// The SVG drawing of the five gyroscope rings of the high-quality orb (UI catalog, section 2), computed once
/// in C# in a 1000 × 1000 view box centred on (500, 500). Angles are in degrees, clockwise from the right
/// (screen axes), like the prototype.
/// </summary>
public static class OrbGyroscopeGeometry
{
    /// <summary>Centre of the view box.</summary>
    public const double Centre = 500;

    /// <summary>
    /// The five rings, outermost first, with their spin speed (UI catalog, section 2). Their size and tilt are
    /// layout: they live in the style sheet, by kind.
    /// </summary>
    public static IReadOnlyList<GyroscopeRing> Rings { get; } =
    [
        new("scale", 0.12, Flat: false),
        new("arcs", -0.35, Flat: false),
        new("dash", 0.5, Flat: false),
        new("band", -0.7, Flat: false),
        new("inner", 0.9, Flat: true),
    ];

    /// <summary>Speed at which the whole gyroscope turns on its vertical axis, in rad/s at speed 1.</summary>
    public const double WorldSpin = 0.18;

    /// <summary>Degree scale: 180 ticks, every 5th and 15th longer, as two paths (fine, then major).</summary>
    public static (string Fine, string Major) ScaleTicks { get; } = BuildScaleTicks();

    /// <summary>Degree labels of the scale ring, every 30°.</summary>
    public static IReadOnlyList<ScaleLabel> ScaleLabels { get; } = BuildScaleLabels();

    /// <summary>The thick arcs of the second ring: path, stroke width and opacity.</summary>
    public static IReadOnlyList<(string Path, double Width, double Opacity)> Arcs { get; } =
    [
        (Arc(455, 200, 300), 26, 0.75),
        (Arc(455, 320, 345), 26, 0.5),
        (Arc(455, 20, 110), 14, 0.9),
        (Arc(455, 130, 150), 14, 0.5),
        (Arc(455, 160, 175), 14, 0.35),
    ];

    /// <summary>The four nodes of the dashed ring, at 0°, 90°, 180° and 270°.</summary>
    public static IReadOnlyList<(double X, double Y)> DashNodes { get; } =
        [Point(470, 0), Point(470, 90), Point(470, 180), Point(470, 270)];

    /// <summary>The 24 marks of the gradient band, as one path of small rotated rectangles.</summary>
    public static string BandMarks { get; } = BuildBandMarks();

    /// <summary>The inner crown: 72 ticks, every 6th longer, as two paths (minor, then major).</summary>
    public static (string Minor, string Major) InnerTicks { get; } = BuildInnerTicks();

    /// <summary>A number for an SVG attribute, independent of the culture.</summary>
    /// <param name="value">The number.</param>
    /// <returns>At most two decimals.</returns>
    public static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>A point on a circle.</summary>
    /// <param name="radius">Radius in view box units.</param>
    /// <param name="angle">Angle in degrees, clockwise from the right.</param>
    /// <returns>The coordinates.</returns>
    public static (double X, double Y) Point(double radius, double angle)
    {
        var radians = angle * Math.PI / 180;
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
        return $"M{N(x1)},{N(y1)} A{N(radius)},{N(radius)} 0 {large} 1 {N(x2)},{N(y2)}";
    }

    private static string Segment(double outer, double inner, double angle)
    {
        var (x1, y1) = Point(outer, angle);
        var (x2, y2) = Point(inner, angle);
        return $"M{N(x1)},{N(y1)} L{N(x2)},{N(y2)}";
    }

    private static (string, string) BuildScaleTicks()
    {
        var fine = new StringBuilder();
        var major = new StringBuilder();
        for (var i = 0; i < 180; i++)
        {
            var angle = i * 2.0;
            if (i % 15 == 0)
            {
                major.Append(Segment(470, 430, angle)).Append(' ');
            }
            else
            {
                fine.Append(Segment(470, 470 - (i % 5 == 0 ? 24 : 12), angle)).Append(' ');
            }
        }

        return (fine.ToString().TrimEnd(), major.ToString().TrimEnd());
    }

    private static List<ScaleLabel> BuildScaleLabels()
    {
        var labels = new List<ScaleLabel>();
        for (var degrees = 0; degrees < 360; degrees += 30)
        {
            var (x, y) = Point(405, degrees);
            labels.Add(new(N(x), N(y), $"rotate({N(degrees + 90)} {N(x)} {N(y)})", $"{degrees}°"));
        }

        return labels;
    }

    private static string BuildBandMarks()
    {
        var path = new StringBuilder();
        for (var i = 0; i < 24; i++)
        {
            // A 12 × 28 rectangle centred on the band, turned with it.
            var angle = i * 15.0;
            var (cx, cy) = Point(440, angle);
            var radians = angle * Math.PI / 180;
            var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
            (double, double) Corner(double dx, double dy) => (cx + (dx * cos) - (dy * sin), cy + (dx * sin) + (dy * cos));
            var corners = new[] { Corner(-6, -14), Corner(6, -14), Corner(6, 14), Corner(-6, 14) };
            path.Append('M').Append(N(corners[0].Item1)).Append(',').Append(N(corners[0].Item2));
            foreach (var (x, y) in corners.Skip(1))
            {
                path.Append(" L").Append(N(x)).Append(',').Append(N(y));
            }

            path.Append(" Z ");
        }

        return path.ToString().TrimEnd();
    }

    private static (string, string) BuildInnerTicks()
    {
        var minor = new StringBuilder();
        var major = new StringBuilder();
        for (var i = 0; i < 72; i++)
        {
            var angle = i * 5.0;
            if (i % 6 == 0)
            {
                major.Append(Segment(470, 420, angle)).Append(' ');
            }
            else
            {
                minor.Append(Segment(470, 445, angle)).Append(' ');
            }
        }

        return (minor.ToString().TrimEnd(), major.ToString().TrimEnd());
    }
}

/// <summary>One gyroscope ring of the high-quality orb (UI catalog, section 2).</summary>
/// <param name="Kind">Which drawing: <c>scale</c>, <c>arcs</c>, <c>dash</c>, <c>band</c> or <c>inner</c>.</param>
/// <param name="Spin">Its speed on itself, in rad/s at speed 1; negative turns the other way.</param>
/// <param name="Flat">Whether it faces the screen, outside the turning gyroscope.</param>
public sealed record GyroscopeRing(string Kind, double Spin, bool Flat)
{
    /// <summary>Milliseconds per turn at speed 1, for the Web Animations API.</summary>
    public string TurnMilliseconds => OrbGyroscopeGeometry.N(Math.Round(2 * Math.PI / Math.Abs(Spin) * 1000));

    /// <summary>1 clockwise, -1 the other way.</summary>
    public int Direction => Math.Sign(Spin);
}

/// <summary>A degree label of the scale ring.</summary>
/// <param name="X">Horizontal position.</param>
/// <param name="Y">Vertical position.</param>
/// <param name="Transform">Its rotation, tangent to the ring.</param>
/// <param name="Text">The degrees.</param>
public sealed record ScaleLabel(string X, string Y, string Transform, string Text);
