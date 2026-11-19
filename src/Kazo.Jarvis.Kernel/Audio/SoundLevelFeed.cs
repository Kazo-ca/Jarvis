namespace Kazo.Jarvis.Kernel.Audio;

/// <summary>
/// The kernel implementation of the sound level contract: clamps, throttles to 30 values per second per
/// origin, and passes the values on to the subscribers. Holds no audio and no device.
/// </summary>
public sealed class SoundLevelFeed : ISoundLevelSink, ISoundLevelSource
{
    /// <summary>At most 30 values per second per origin (ADR 0005, section 5).</summary>
    public const int MaximumRatePerSecond = 30;

    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(1.0 / MaximumRatePerSecond);

    private readonly TimeProvider time;
    private readonly Lock gate = new();
    private readonly long?[] lastPassed = new long?[Enum.GetValues<SoundOrigin>().Length];
    private Action<SoundLevel>[] handlers = [];

    /// <summary>Creates the feed.</summary>
    /// <param name="time">Clock used for the throttling; a fake one in the tests.</param>
    public SoundLevelFeed(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        this.time = time;
    }

    /// <inheritdoc />
    public void Report(SoundOrigin origin, float value)
    {
        var level = new SoundLevel(origin, float.IsNaN(value) ? 0f : Math.Clamp(value, 0f, 1f));
        Action<SoundLevel>[] targets;
        lock (gate)
        {
            var now = time.GetTimestamp();
            var last = lastPassed[(int)origin];
            if (level.Value > 0f && last is { } previous && time.GetElapsedTime(previous, now) < MinimumInterval)
            {
                return;
            }

            lastPassed[(int)origin] = now;
            targets = handlers;
        }

        foreach (var handler in targets)
        {
            handler(level);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action<SoundLevel> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (gate)
        {
            handlers = [.. handlers, handler];
        }

        return new Unsubscriber(() =>
        {
            lock (gate)
            {
                handlers = [.. handlers.Where(existing => existing != handler)];
            }
        });
    }

    private sealed class Unsubscriber(Action dispose) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                dispose();
            }
        }
    }
}
