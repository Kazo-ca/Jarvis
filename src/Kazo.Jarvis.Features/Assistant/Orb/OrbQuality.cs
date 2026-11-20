namespace Kazo.Jarvis.Features.Assistant.Orb;

/// <summary>The two versions of the orb (ADR 0011, section 1), chosen in the settings.</summary>
public enum OrbQuality
{
    /// <summary>Plasma sphere, binary planet clouds and five gyroscope rings (UI catalog, section 2). The default.</summary>
    High,

    /// <summary>Still sphere and three open rings (UI catalog, section 3), for machines where the first costs too much.</summary>
    Light,
}
