using Kazo.Jarvis.Kernel.Desktop;

namespace Kazo.Jarvis.Features.Tasks.Fan;

/// <summary>
/// Where the panels of the fan go (ADR 0010, section 1): on an arc around the orb, toward the centre of the
/// screen. Lengths are in <c>em</c> of the fan, whose font size follows the size of the surface, so the same
/// arrangement fits a small screen and a large one.
/// </summary>
public static class FanLayout
{
    /// <summary>The radius of the arc, from the centre of the orb to the centre of a panel.</summary>
    public const double Radius = 46;

    /// <summary>The width of a panel.</summary>
    public const double PanelWidth = 17;

    /// <summary>The height of a panel, before its details are shown.</summary>
    public const double PanelHeight = 11;

    /// <summary>How many panels the arc holds; the others are counted in one line (provisional: ADR 0010, section 9, leaves it open).</summary>
    public const int MostPanels = 5;

    // Angles from the horizontal, in degrees, per number of panels. A panel is wider than it is tall, so the
    // panels near the horizontal stack one above the other and the last ones stand side by side: an even spread
    // would make them overlap.
    private static readonly double[][] Angles =
    [
        [40],
        [28, 54],
        [18.5, 34.5, 54.5],
        [10, 25, 42, 66.5],
        [4, 18.5, 34.5, 54.5, 78.5],
    ];

    /// <summary>
    /// The side the fan opens to: toward the centre of the screen, so the arrangement of the nearest corner
    /// (ADR 0010, section 1).
    /// </summary>
    /// <param name="anchor">The centre of the orb on the surface.</param>
    /// <returns>-1 or 1 on each axis: left or right, up or down.</returns>
    public static (int Horizontal, int Vertical) Direction(FanAnchor anchor) =>
        (anchor.X >= 0.5 ? -1 : 1, anchor.Y >= 0.5 ? -1 : 1);

    /// <summary>The centres of <paramref name="count"/> panels, relative to the centre of the orb, the first one nearest the horizontal.</summary>
    /// <param name="anchor">The centre of the orb on the surface.</param>
    /// <param name="count">How many panels; at most <see cref="MostPanels"/> are placed.</param>
    /// <returns>One slot per panel.</returns>
    public static IReadOnlyList<FanSlot> Slots(FanAnchor anchor, int count)
    {
        var shown = Math.Clamp(count, 0, MostPanels);
        if (shown == 0)
        {
            return [];
        }

        var (horizontal, vertical) = Direction(anchor);
        return [.. Angles[shown - 1].Select(angle =>
        {
            var radians = angle * Math.PI / 180;
            return new FanSlot(
                Math.Round(horizontal * Radius * Math.Cos(radians), 2),
                Math.Round(vertical * Radius * Math.Sin(radians), 2));
        })];
    }
}

/// <summary>The centre of a panel, in <c>em</c>, relative to the centre of the orb.</summary>
/// <param name="X">To the right when positive.</param>
/// <param name="Y">Down when positive.</param>
public readonly record struct FanSlot(double X, double Y);
