using Kazo.Jarvis.Kernel.Assistant;

namespace Kazo.Jarvis.Features.Assistant.Orb;

/// <summary>
/// How the orb moves in one look (ADR 0011, sections 3 and 5; UI catalog, section 4). The rule lives here, in
/// C#; the scripts only apply it (ADR 0005, section 5).
/// </summary>
/// <param name="Rate">
/// Speed of the rotation, 1 being the catalog's "speed 1" (light version: inner ring 24 s per turn; high
/// quality: the ring speeds of the catalog's section 2 table). 0 stops the rotation: the orb is then a still
/// image.
/// </param>
/// <param name="Pulses">Whether each ring pulses at random on its own (speaking, light version only).</param>
public readonly record struct OrbMotion(double Rate, bool Pulses)
{
    /// <summary>Rotation speed of an animated idle look, light version, offered by the Web demonstration only.</summary>
    public const double AnimatedIdleRate = 0.3;

    /// <summary>Rotation speed of an animated idle look, high quality, offered by the Web demonstration only.</summary>
    public const double AnimatedIdleRateHighQuality = 0.2;

    /// <summary>No movement at all.</summary>
    public static OrbMotion Still { get; } = new(0, false);

    /// <summary>Whether nothing moves: no rotation and no pulse.</summary>
    public bool IsStill => Rate == 0 && !Pulses;

    /// <summary>The motion of a look, light version.</summary>
    /// <param name="look">The look shown.</param>
    /// <param name="animateIdle">The host's animation rule, see <see cref="For(AssistantLook, OrbQuality, bool, bool)"/>.</param>
    /// <param name="reducedMotion">The "reduced motion" option: no rotation and no pulse at all.</param>
    /// <returns>The rotation speed and whether the rings pulse.</returns>
    public static OrbMotion For(AssistantLook look, bool animateIdle, bool reducedMotion) =>
        For(look, OrbQuality.Light, animateIdle, reducedMotion);

    /// <summary>The motion of a look.</summary>
    /// <param name="look">The look shown.</param>
    /// <param name="quality">The version of the orb: each has its speeds (UI catalog, section 4).</param>
    /// <param name="animateIdle">
    /// The host's animation rule: <see langword="false"/> keeps the idle look still (Windows, ADR 0005,
    /// section 6); the Web demonstration lets the user try <see langword="true"/>.
    /// </param>
    /// <param name="reducedMotion">The "reduced motion" option: no rotation and no pulse at all.</param>
    /// <returns>The rotation speed and whether the rings pulse.</returns>
    public static OrbMotion For(AssistantLook look, OrbQuality quality, bool animateIdle, bool reducedMotion)
    {
        if (reducedMotion)
        {
            return Still;
        }

        var high = quality == OrbQuality.High;
        return look switch
        {
            AssistantLook.Idle when !animateIdle => Still,
            AssistantLook.Idle => new(high ? AnimatedIdleRateHighQuality : AnimatedIdleRate, false),
            AssistantLook.Listening => new(high ? 0.8 : 1, false),
            AssistantLook.Thinking => new(high ? 2.4 : 3.5, false),

            // High quality: the light follows the voice, nothing pulses (ADR 0011, section 3).
            AssistantLook.Speaking => high ? new(1, false) : new(1.2, true),
            AssistantLook.LongTask => new(high ? 0.45 : 0.5, false),

            // ADR 0023, section 1: something waits for the user. Slow and pulsing, to call the eye without
            // the hurry of the thinking look; animated like an active phase.
            AssistantLook.InteractionRequired => new(high ? 0.6 : 0.7, true),

            // ADR 0011, section 5: a flash on entry (drawn by the style sheet), then a still image. The
            // catalog's 0.25 rotation predates the idle budget and is not kept.
            AssistantLook.Error => Still,
            AssistantLook.MicrophoneMuted => Still,
            _ => Still,
        };
    }
}
