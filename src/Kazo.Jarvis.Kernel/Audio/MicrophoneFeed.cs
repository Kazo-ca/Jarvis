namespace Kazo.Jarvis.Kernel.Audio;

/// <summary>
/// The kernel implementation of the microphone stream: a ring buffer in memory and a list of subscribers.
/// Holds no device; the audio slice writes to it, the voice slices read from it (ADR 0006, section 1).
/// </summary>
public sealed class MicrophoneFeed : IMicrophoneSink, IMicrophoneSource
{
    /// <summary>
    /// Length of the listening buffer. Not set by an ADR (work item stage1/8-audio-devices): enough for the
    /// wake word window and a long utterance, about 2 MB.
    /// </summary>
    public static readonly TimeSpan DefaultBufferLength = TimeSpan.FromSeconds(30);

    private readonly Lock gate = new();
    private readonly float[] ring;
    private long written;
    private int stored;
    private SamplesHandler[] handlers = [];

    /// <summary>Creates the feed with <see cref="DefaultBufferLength"/>.</summary>
    public MicrophoneFeed()
        : this(DefaultBufferLength)
    {
    }

    /// <summary>Creates the feed.</summary>
    /// <param name="bufferLength">How long the listening buffer keeps; at least one sample.</param>
    public MicrophoneFeed(TimeSpan bufferLength)
    {
        var capacity = MicrophoneFormat.SamplesFor(bufferLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1, nameof(bufferLength));
        ring = new float[capacity];
        BufferLength = bufferLength;
    }

    /// <inheritdoc />
    public TimeSpan BufferLength { get; }

    /// <inheritdoc />
    public long Written => Interlocked.Read(ref written);

    /// <inheritdoc />
    public void Write(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
        {
            return;
        }

        SamplesHandler[] targets;
        lock (gate)
        {
            // Only the end of a block longer than the ring can be kept.
            var kept = samples.Length > ring.Length ? samples[^ring.Length..] : samples;
            var start = (int)((written + samples.Length - kept.Length) % ring.Length);
            var first = Math.Min(kept.Length, ring.Length - start);
            kept[..first].CopyTo(ring.AsSpan(start));
            kept[first..].CopyTo(ring);
            Interlocked.Add(ref written, samples.Length);
            stored = Math.Min(ring.Length, stored + kept.Length);
            targets = handlers;
        }

        foreach (var handler in targets)
        {
            handler(samples);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (gate)
        {
            // Overwrite: what was heard does not stay in memory after a mute (ADR 0006, section 1).
            Array.Clear(ring);
            stored = 0;
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(SamplesHandler handler)
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

    /// <inheritdoc />
    public int CopyLatest(Span<float> destination)
    {
        lock (gate)
        {
            var count = Math.Min(destination.Length, stored);
            var end = (int)(written % ring.Length);
            var start = (end - count + ring.Length) % ring.Length;
            var target = destination[^count..];
            var first = Math.Min(count, ring.Length - start);
            ring.AsSpan(start, first).CopyTo(target);
            ring.AsSpan(0, count - first).CopyTo(target[first..]);
            return count;
        }
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
