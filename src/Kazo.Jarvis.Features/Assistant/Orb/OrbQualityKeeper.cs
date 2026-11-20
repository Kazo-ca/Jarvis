using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Logging;

namespace Kazo.Jarvis.Features.Assistant.Orb;

/// <summary>
/// Keeps the "orb quality" setting (ADR 0011, section 1). Watches the setting, so a change applies to the
/// shown orb at once, without a restart. Jarvis never changes the version on its own.
/// </summary>
public sealed partial class OrbQualityKeeper : IDisposable
{
    private readonly ISettingsStore settings;
    private readonly ILogger<OrbQualityKeeper> logger;
    private readonly IDisposable watch;
    private int current; // An OrbQuality, as int for Interlocked.

    /// <summary>Creates the keeper and reads the setting.</summary>
    /// <param name="settings">The settings, for <see cref="OrbOptions.Section"/>.</param>
    /// <param name="logger">Logs the changes.</param>
    public OrbQualityKeeper(ISettingsStore settings, ILogger<OrbQualityKeeper> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
        this.logger = logger;

        // Watch before reading: a save between the two is not missed.
        watch = settings.Watch(OrbOptions.Section, Apply);
        current = (int)settings.Read(OrbOptions.Section).Quality;
    }

    /// <summary>Raised after the quality changed, with the new value. May be raised on any thread.</summary>
    public event Action<OrbQuality>? Changed;

    /// <summary>The version of the orb to show.</summary>
    public OrbQuality Current => (OrbQuality)Volatile.Read(ref current);

    /// <summary>Saves the choice of the user; the shown orb changes at once.</summary>
    /// <param name="quality">The chosen version.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>Whether the choice was saved, and why not.</returns>
    public ValueTask<SettingsWriteResult> ChooseAsync(OrbQuality quality, CancellationToken cancellationToken = default) =>
        settings.WriteAsync(OrbOptions.Section, new OrbOptions { Quality = quality }, cancellationToken);

    /// <inheritdoc />
    public void Dispose() => watch.Dispose();

    private void Apply(OrbOptions options)
    {
        var next = options.Quality;
        var previous = (OrbQuality)Interlocked.Exchange(ref current, (int)next);
        if (previous == next)
        {
            return;
        }

        LogChanged(previous, next);
        Changed?.Invoke(next);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Orb quality changed: {Previous} -> {Current}.")]
    private partial void LogChanged(OrbQuality previous, OrbQuality current);
}
