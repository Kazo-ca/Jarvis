using Kazo.Jarvis.Kernel.Assistant;

namespace Kazo.Jarvis.Features.Assistant.Orb;

/// <summary>
/// How the light orb moves in one look (ADR 0011, sections 3 and 5; UI catalog, section 4). The rule lives
/// here, in C#; the script only applies it (ADR 0005, section 5).
/// </summary>
/// <param name="Rate">
/// Speed of the ring rotation, 1 being the catalog's "speed 1" (inner ring 24 s per turn). 0 stops the
/// rotation: the orb is then a still image.
/// </param>
/// <param name="Pulses">Whether each ring pulses at random on its own (speaking, light version).</param>
public readonly record struct OrbMotion(double Rate, bool Pulses)
{
    /// <summary>Rotation speed of an animated idle look, offered by the Web demonstration only.</summary>
    public const double AnimatedIdleRate = 0.3;

    /// <summary>No movement at all.</summary>
    public static OrbMotion Still { get; } = new(0, false);

    /// <summary>Whether nothing moves: no rotation and no pulse.</summary>
    public bool IsStill => Rate == 0 && !Pulses;

    /// <summary>The motion of a look.</summary>
    /// <param name="look">The look shown.</param>
    /// <param name="animateIdle">
    /// The host's animation rule: <see langword="false"/> keeps the idle look still (Windows, ADR 0005,
    /// section 6); the Web demonstration lets the user try <see langword="true"/>.
    /// </param>
    /// <param name="reducedMotion">The "reduced motion" option: no rotation and no pulse at all.</param>
    /// <returns>The rotation speed and whether the rings pulse.</returns>
    public static OrbMotion For(AssistantLook look, bool animateIdle, bool reducedMotion)
    {
        if (reducedMotion)
        {
            return Still;
        }

        return look switch
        {
            AssistantLook.Idle => animateIdle ? new(AnimatedIdleRate, false) : Still,
            AssistantLook.Listening => new(1, false),
            AssistantLook.Thinking => new(3.5, false),
            AssistantLook.Speaking => new(1.2, true),
            AssistantLook.LongTask => new(0.5, false),

            // ADR 0011, section 5: a flash on entry (drawn by the style sheet), then a still image. The
            // catalog's 0.25 rotation predates the idle budget and is not kept.
            AssistantLook.Error => Still,
            AssistantLook.MicrophoneMuted => Still,
            _ => Still,
        };
    }
}
