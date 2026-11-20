using Kazo.Jarvis.Kernel.Settings;
using Microsoft.Extensions.Hosting;

namespace Kazo.Jarvis.Features.Maintenance.Updates;

/// <summary>
/// The automatic check (US1.18), only when <see cref="UpdateOptions.Automatic"/> is on: a minute after
/// start, so that start-up stays fast, then every 6 hours. Releases are rare and GitHub limits requests
/// without a token to 60 an hour. The setting is read at each tick, so turning it on or off needs no restart.
/// </summary>
public sealed class AutomaticUpdateCheck : BackgroundService
{
    /// <summary>Delay before the first check.</summary>
    public static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(1);

    /// <summary>Delay between two checks.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly UpdateService updates;
    private readonly ISettingsStore settings;
    private readonly TimeProvider time;

    /// <summary>Creates the background check.</summary>
    /// <param name="updates">The updates slice.</param>
    /// <param name="settings">The settings, for <see cref="UpdateOptions.Section"/>.</param>
    /// <param name="time">The clock.</param>
    public AutomaticUpdateCheck(UpdateService updates, ISettingsStore settings, TimeProvider time)
    {
        this.updates = updates;
        this.settings = settings;
        this.time = time;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstDelay, time, stoppingToken).ConfigureAwait(false);
            while (!stoppingToken.IsCancellationRequested)
            {
                if (settings.Read(UpdateOptions.Section).Automatic)
                {
                    // Never throws but for cancellation: failures are logged by the service.
                    await updates.CheckAsync(automatic: true, stoppingToken).ConfigureAwait(false);
                }

                await Task.Delay(Interval, time, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
