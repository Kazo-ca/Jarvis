using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.Voice.WakeWord;

/// <summary>Settings of the wake word (US1.5), section <c>Voice.WakeWord</c> of <c>config.json</c>.</summary>
public sealed record WakeWordOptions
{
    /// <summary>Default threshold (ADR 0006, section 2), the usual openWakeWord value.</summary>
    public const float DefaultThreshold = 0.5f;

    /// <summary>Lowest threshold offered: below it, ordinary speech wakes Jarvis.</summary>
    public const float MinimumThreshold = 0.05f;

    /// <summary>Highest threshold offered: above it, even a clear "hey jarvis" is often missed.</summary>
    public const float MaximumThreshold = 0.95f;

    /// <summary>The section declared by this slice.</summary>
    public static SettingsSection<WakeWordOptions> Section { get; } = new("Voice.WakeWord", () => new WakeWordOptions());

    /// <summary>
    /// Score from which "hey jarvis" is detected, from <see cref="MinimumThreshold"/> to
    /// <see cref="MaximumThreshold"/>. Tuned with Benoit's voice during stage 1 (ADR 0006, section 2).
    /// </summary>
    public float Threshold { get; init; } = DefaultThreshold;

    /// <summary><see cref="Threshold"/> brought within the offered range (a hand-edited file may hold anything).</summary>
    public float EffectiveThreshold =>
        float.IsFinite(Threshold) ? Math.Clamp(Threshold, MinimumThreshold, MaximumThreshold) : DefaultThreshold;
}
