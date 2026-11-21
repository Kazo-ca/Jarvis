using Kazo.Jarvis.Kernel.Settings;

namespace Kazo.Jarvis.Features.ClaudeCode.Announcements;

/// <summary>
/// When nothing is said (ADR 0021, section 9): during the quiet hours of the settings, and during the quiet
/// period the user accepted for once (« je vais dormir »). That period is kept in memory only: it changes no
/// setting, ends by itself, and ends when Jarvis closes.
/// </summary>
public sealed class QuietTime
{
    /// <summary>The length of the quiet period Jarvis offers.</summary>
    public const int DefaultHours = 8;

    /// <summary>The longest quiet period that can be started.</summary>
    public const int MostHours = 24;

    private readonly ISettingsStore settings;
    private readonly TimeProvider time;
    private readonly Lock gate = new();
    private DateTimeOffset? until;

    /// <summary>Creates the keeper of the silence.</summary>
    /// <param name="settings">The settings, for the quiet hours.</param>
    /// <param name="time">The clock and the time zone of the PC.</param>
    public QuietTime(ISettingsStore settings, TimeProvider time)
    {
        this.settings = settings;
        this.time = time;
    }

    /// <summary>Raised after a quiet period started or was ended by the user; on any thread.</summary>
    public event Action? Changed;

    /// <summary>When the quiet period of the moment ends, or <see langword="null"/> when there is none.</summary>
    public DateTimeOffset? PeriodEnd
    {
        get
        {
            lock (gate)
            {
                return until is { } end && end > time.GetUtcNow() ? end : null;
            }
        }
    }

    /// <summary>Whether quiet hours are set.</summary>
    public bool HasHours => settings.Read(ClaudeAnnouncementsOptions.Section).QuietHours is { Count: > 0 };

    /// <summary>Whether nothing is said now.</summary>
    public bool IsQuiet =>
        PeriodEnd is not null
        || settings.Read(ClaudeAnnouncementsOptions.Section).IsQuiet(TimeOnly.FromDateTime(time.GetLocalNow().DateTime));

    /// <summary>Starts a quiet period from now. A period already running is replaced.</summary>
    /// <param name="hours">Its length, from 1 to <see cref="MostHours"/>.</param>
    /// <returns>When it ends.</returns>
    public DateTimeOffset Start(double hours)
    {
        var end = time.GetUtcNow() + TimeSpan.FromHours(Math.Clamp(hours, 1, MostHours));
        lock (gate)
        {
            until = end;
        }

        Changed?.Invoke();
        return end;
    }

    /// <summary>Ends the quiet period before its time. The quiet hours of the settings are not touched.</summary>
    public void End()
    {
        lock (gate)
        {
            until = null;
        }

        Changed?.Invoke();
    }
}
